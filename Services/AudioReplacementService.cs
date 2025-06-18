using System.Diagnostics;
using NAudio.Wave;
using NAudio.Lame;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace os.Services
{
    public class AudioReplacementService
    {
        private readonly DbConnectionService _dbConnectionService;
        private readonly ITranscriptionService _transcriptionService;

        public AudioReplacementService(
            DbConnectionService dbConnectionService,
            ITranscriptionService transcriptionService)
        {
            _dbConnectionService = dbConnectionService;
            _transcriptionService = transcriptionService;
        }

        private async Task<string> LoadTranscriptionFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentNullException(nameof(fileName));

            string transcriptionsFolder = Path.Combine("wwwroot", "transcriptions");
            string filePath = Path.Combine(transcriptionsFolder, fileName);

            if (!System.IO.File.Exists(filePath))
                throw new FileNotFoundException($"Transcription file not found: {filePath}");

            return await System.IO.File.ReadAllTextAsync(filePath);
        }

        public async Task<string> ReplaceAudioAsync()
        {
            // Transcription, time stamp identification and name removal service
            string updatedAudioPath = string.Empty;
            try
            {
                var speaker = _dbConnectionService.GetSpeakerById(44);
                //string transcription = "";
                //if (speaker != null)
                //{
                //    // Add the force parameter to ensure it uses the latest implementation
                //    transcription = await _transcriptionService.TranscribeSpeakerMp3(speaker, force: true);
                //    ViewBag.Transcription = transcription;
                //    ViewBag.TranscriptionInfo = $"Transcribed using {_transcriptionService.GetType().Name} with WhisperS2T";
                //}


                using var httpClient = new HttpClient();
                var extractor = new SpeakerExtractor(httpClient);



                //// Extract the filename from the transcription string
                //string fileName = string.Empty;
                //if (transcription.StartsWith("# Transcription of "))
                //{
                //    // Find the text between "# Transcription of " and the next newline or .mp3
                //    int startIndex = "# Transcription of ".Length;
                //    int endIndex = transcription.IndexOf("\r\n", startIndex);

                //    if (endIndex > startIndex)
                //    {
                //        // Extract the full filename (including .mp3 extension)
                //        string fullFileName = transcription.Substring(startIndex, endIndex - startIndex);

                //        // Store the extracted filename
                //        fileName = fullFileName;
                //        fileName = Path.GetFileNameWithoutExtension(fullFileName);
                //        fileName += "_transcript.txt";
                //    }
                //}
                //// Load the transcript from memory
                //string fullTranscriptText = await LoadTranscriptionFile(fileName);

                // Load the transcript file
                string fullTranscriptText = await LoadTranscriptionFile("Boston_F_1749991986760_transcript.txt");

                // Extract only the segments part (everything after "## Segments")
                string transcriptContext = "";
                int segmentsIndex = fullTranscriptText.IndexOf("## Segments");
                if (segmentsIndex >= 0)
                {
                    transcriptContext = fullTranscriptText.Substring(segmentsIndex);

                    // Approximate token count (roughly 4 characters per token for English text)
                    int approximateTokenCount = transcriptContext.Length / 4;
                    Debug.WriteLine($"Segments token count (approximate): {approximateTokenCount}");

                    // If the text is too long, trim it
                    const int maxTokens = 1000;
                    if (approximateTokenCount > maxTokens)
                    {
                        // Calculate approximately how many characters to keep
                        int maxChars = maxTokens * 4;

                        // Look for a segment break to trim at for cleaner cutting
                        int lastSegmentBreakPos = transcriptContext.LastIndexOf("[", maxChars);

                        // Choose the best position to trim at
                        int trimPosition = (lastSegmentBreakPos > 0) ? lastSegmentBreakPos : maxChars;

                        // Trim the text
                        transcriptContext = transcriptContext.Substring(0, trimPosition);
                        Debug.WriteLine($"Trimmed transcript to approximately {trimPosition / 4} tokens");
                    }
                }
                else
                {
                    // If "## Segments" is not found, use a shorter portion of the transcript
                    transcriptContext = fullTranscriptText.Substring(0, Math.Min(fullTranscriptText.Length, 48000)); // 12K tokens
                    Debug.WriteLine("Warning: No segments marker found in transcript. Using limited portion.");
                }


                // Get names and timestamps
                List<CutSpeakerModel> sanitizedNames = new List<CutSpeakerModel>();
                try
                {
                    List<CutSpeakerModel> names = await extractor.ExtractSpeakersAsync(transcriptContext);
                    List<string> excludeNamesList = new List<string>
                    {
                        "Dr. Bob",
                        "Dr Bob",
                        "Bill Wilson",
                        "Henrietta"
                    };

                    foreach (var name in names)
                    {
                        // Only add the name if it's not in the exclusion list
                        if (name != null && name.Name != null && !excludeNamesList.Contains(name.Name))
                        {
                            sanitizedNames.Add(name);
                            Debug.WriteLine($"Name found: {name.Name}  Begins: {name.Start} Ends: {name.End}");
                            Debug.WriteLine("");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }

                // add loop to remove .5 seconds from each name's begining time and add .5 seconds to the end time
                for (int i = 0; i < sanitizedNames.Count; i++)
                {
                    // Parse the start and end timestamps
                    if (TryParseTimestamp(sanitizedNames[i].Start, out TimeSpan startTime) &&
                        TryParseTimestamp(sanitizedNames[i].End, out TimeSpan endTime))
                    {
                        // Adjust the start time by subtracting 0.5 seconds
                        startTime = startTime.Subtract(TimeSpan.FromSeconds(0.5));
                        // Adjust the end time by adding 0.5 seconds
                        endTime = endTime.Add(TimeSpan.FromSeconds(0.25));
                        // Update the names with adjusted times
                        sanitizedNames[i].Start = startTime.ToString(@"hh\:mm\:ss\.fff");
                        //sanitizedNames[i].End = endTime.ToString(@"hh\:mm\:ss\.fff");
                    }
                }


                // Process the audio file if names were found
                if (sanitizedNames.Count > 0)
                {
                    try
                    {
                        // Get the original audio file path
                        string originalMp3Path = Path.Combine("wwwroot", "uploads", speaker.SecretFileName);
                        string replacementAudioPath = Path.Combine("wwwroot", "sounds", "replacement_audio.mp3");

                        // Output path for the new audio file
                        string outputMp3Path = Path.Combine("wwwroot", "uploads", $"anonymized_{speaker.SecretFileName}");

                        // Check if the audio files exist
                        if (!System.IO.File.Exists(originalMp3Path))
                        {
                            Debug.WriteLine($"Original audio file not found: {originalMp3Path}");
                            return string.Empty;
                        }
                        else if (!System.IO.File.Exists(replacementAudioPath))
                        {
                            Debug.WriteLine($"Replacement audio file not found: {replacementAudioPath}");
                            return string.Empty;
                        }

                        // Create a backup of the original file
                        string backupPath = originalMp3Path + ".backup";
                        File.Copy(originalMp3Path, backupPath, true);

                        try
                        {
                            // Get bitrate and other properties of the original MP3
                            Mp3FileProperties originalProperties = GetMp3Properties(originalMp3Path);
                            Mp3FileProperties replacementProperties = GetMp3Properties(replacementAudioPath);

                            // Process the MP3 files directly
                            using (var tempFileStream = new FileStream(outputMp3Path, FileMode.Create))
                            {
                                // Read the original MP3 file
                                byte[] originalBytes = File.ReadAllBytes(originalMp3Path);

                                // Read the replacement MP3 file
                                byte[] replacementBytes = ExtractMp3AudioData(replacementAudioPath);

                                // Sort segments by start time
                                var segments = sanitizedNames.OrderBy(s => ParseTimestampToSeconds(s.Start)).ToList();

                                // Get time-based positions in the original MP3
                                List<Mp3Segment> mp3Segments = CalculateMp3Segments(
                                    originalBytes,
                                    segments,
                                    originalProperties.DurationSeconds);

                                // Process each segment and build the output file
                                long currentPos = 0;

                                // Copy ID3 header if present
                                if (HasId3Header(originalBytes))
                                {
                                    int id3Size = GetId3HeaderSize(originalBytes);
                                    tempFileStream.Write(originalBytes, 0, id3Size);
                                    currentPos = id3Size;
                                }

                                foreach (var segment in mp3Segments)
                                {
                                    // Copy original content up to the segment start
                                    if (segment.StartPosition > currentPos)
                                    {
                                        tempFileStream.Write(originalBytes, (int)currentPos, (int)(segment.StartPosition - currentPos));
                                    }

                                    // Prepare replacement audio data
                                    byte[] modifiedReplacement = GetReplacementSegment(
                                        replacementBytes,
                                        segment.DurationSeconds,
                                        originalProperties.BitrateKbps);

                                    // Write replacement data
                                    tempFileStream.Write(modifiedReplacement, 0, modifiedReplacement.Length);

                                    // Update current position
                                    currentPos = segment.EndPosition;
                                }

                                // Copy the remainder of the original file
                                if (currentPos < originalBytes.Length)
                                {
                                    tempFileStream.Write(originalBytes, (int)currentPos, (int)(originalBytes.Length - currentPos));
                                }
                            }

                            Debug.WriteLine($"Audio file processed successfully. Output saved to: {outputMp3Path}");
                            updatedAudioPath = outputMp3Path;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error processing audio: {ex.Message}");

                            // If an error occurs, restore original from backup
                            if (File.Exists(backupPath))
                            {
                                File.Copy(backupPath, outputMp3Path, true);
                                updatedAudioPath = outputMp3Path;
                                Debug.WriteLine("Restored original audio as fallback.");
                            }
                        }

                        // Clean up backup file
                        try
                        {
                            if (File.Exists(backupPath))
                                File.Delete(backupPath);
                        }
                        catch { /* Ignore cleanup errors */ }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error processing audio: {ex.Message}");
                    }
                }
                else
                {
                    Debug.WriteLine("No names found to replace in the audio.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in audio replacement service: {ex.Message}");
            }

            return updatedAudioPath;
        }

        /// <summary>
        /// Gets basic properties of an MP3 file
        /// </summary>
        private Mp3FileProperties GetMp3Properties(string filePath)
        {
            var result = new Mp3FileProperties();

            // Use NAudio to get duration and other properties
            using (var reader = new Mp3FileReader(filePath))
            {
                result.DurationSeconds = reader.TotalTime.TotalSeconds;

                // MP3 file readers don't expose bitrate directly through a property called AverageBitsPerSecond
                // Instead, calculate it from the length and duration
                if (reader.TotalTime.TotalSeconds > 0)
                {
                    // Calculate bitrate in kbps: file size in bits / duration in seconds / 1000
                    long fileSizeInBits = new FileInfo(filePath).Length * 8;
                    result.BitrateKbps = (int)(fileSizeInBits / reader.TotalTime.TotalSeconds / 1000);
                }
                else
                {
                    // Default to common bitrate if we can't calculate
                    result.BitrateKbps = 128;
                }

                result.SampleRate = reader.WaveFormat.SampleRate;
                result.Channels = reader.WaveFormat.Channels;
            }

            return result;
        }

        /// <summary>
        /// Extracts actual audio data from an MP3 file, skipping ID3 headers
        /// </summary>
        private byte[] ExtractMp3AudioData(string filePath)
        {
            byte[] allBytes = File.ReadAllBytes(filePath);

            if (HasId3Header(allBytes))
            {
                int id3Size = GetId3HeaderSize(allBytes);
                byte[] audioData = new byte[allBytes.Length - id3Size];
                Array.Copy(allBytes, id3Size, audioData, 0, audioData.Length);
                return audioData;
            }

            return allBytes;
        }

        /// <summary>
        /// Checks if an MP3 file has an ID3 header
        /// </summary>
        private bool HasId3Header(byte[] data)
        {
            // Check for ID3v2 tag signature "ID3"
            if (data.Length > 10 && data[0] == 0x49 && data[1] == 0x44 && data[2] == 0x33)
                return true;

            return false;
        }

        /// <summary>
        /// Gets the size of the ID3 header in an MP3 file
        /// </summary>
        private int GetId3HeaderSize(byte[] data)
        {
            if (data.Length > 10 && data[0] == 0x49 && data[1] == 0x44 && data[2] == 0x33)
            {
                // ID3v2 tag size is stored as a synchsafe integer in bytes 6-9
                // Each byte uses only 7 bits, with the high bit always 0
                int size = ((data[6] & 0x7F) << 21) |
                           ((data[7] & 0x7F) << 14) |
                           ((data[8] & 0x7F) << 7) |
                           (data[9] & 0x7F);

                // Add 10 bytes for the ID3 header itself
                return size + 10;
            }

            return 0;
        }

        /// <summary>
        /// Converts a timestamp string to seconds
        /// </summary>
        private double ParseTimestampToSeconds(string timestamp)
        {
            if (TryParseTimestamp(timestamp, out TimeSpan result))
                return result.TotalSeconds;

            return 0;
        }

        /// <summary>
        /// Calculates MP3 segment positions based on timestamps
        /// </summary>
        private List<Mp3Segment> CalculateMp3Segments(byte[] mp3Data, List<CutSpeakerModel> segments, double totalDurationSeconds)
        {
            var result = new List<Mp3Segment>();
            int headerOffset = HasId3Header(mp3Data) ? GetId3HeaderSize(mp3Data) : 0;

            // Find all frame positions to ensure we cut at valid boundaries
            List<long> framePositions = FindAllFrameSyncPositions(mp3Data);
            if (framePositions.Count < 2)
            {
                Debug.WriteLine("Warning: Not enough MP3 frames found in original audio");
                return result;
            }

            // Total file size minus header
            long audioDataSize = mp3Data.Length - headerOffset;

            foreach (var segment in segments)
            {
                double startSeconds = ParseTimestampToSeconds(segment.Start);
                double endSeconds = ParseTimestampToSeconds(segment.End);

                if (endSeconds <= startSeconds || startSeconds < 0 || endSeconds > totalDurationSeconds)
                {
                    Debug.WriteLine($"Invalid segment time range: {segment.Start} to {segment.End}");
                    continue;
                }

                // Calculate positions as a ratio of total duration
                double startRatio = startSeconds / totalDurationSeconds;
                double endRatio = endSeconds / totalDurationSeconds;

                // Calculate approximate byte positions
                long approxStartPos = headerOffset + (long)(startRatio * audioDataSize);
                long approxEndPos = headerOffset + (long)(endRatio * audioDataSize);

                // Find nearest frame boundaries from our pre-calculated list
                long adjustedStartPos = FindNearestFramePosition(framePositions, approxStartPos);
                long adjustedEndPos = FindNearestFramePosition(framePositions, approxEndPos);

                if (adjustedStartPos >= adjustedEndPos)
                {
                    Debug.WriteLine($"Could not find valid frame boundaries for segment: {segment.Name}");
                    continue;
                }

                result.Add(new Mp3Segment
                {
                    StartPosition = adjustedStartPos,
                    EndPosition = adjustedEndPos,
                    DurationSeconds = endSeconds - startSeconds,
                    Name = segment.Name
                });
            }

            return result;
        }

        /// <summary>
        /// Finds the nearest frame position from a list of positions
        /// </summary>
        private long FindNearestFramePosition(List<long> framePositions, long targetPosition)
        {
            if (framePositions.Count == 0)
                return targetPosition;

            long nearest = framePositions[0];
            long minDistance = Math.Abs(framePositions[0] - targetPosition);

            foreach (long pos in framePositions)
            {
                long distance = Math.Abs(pos - targetPosition);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = pos;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Gets a portion of the replacement audio with appropriate length
        /// </summary>
        private byte[] GetReplacementSegment(byte[] replacementData, double durationSeconds, int targetBitrateKbps)
        {
            // When working with MP3 data directly, we need to be careful about frame boundaries
            // A more reliable approach is to extract a portion of the replacement audio that's roughly the right size

            // First, find valid MP3 frame boundaries in the replacement audio
            List<long> framePositions = FindAllFrameSyncPositions(replacementData);
            if (framePositions.Count < 2)
            {
                Debug.WriteLine("Warning: Not enough MP3 frames found in replacement audio");
                return replacementData; // Return the entire replacement as fallback
            }

            // Estimate how much data we need based on duration and bitrate
            int estimatedBytes = (int)(durationSeconds * targetBitrateKbps * 125); // 125 = 1000/8

            // Find the appropriate ending frame position
            int endFrameIndex = 0;
            for (int i = 0; i < framePositions.Count; i++)
            {
                if (framePositions[i] > estimatedBytes)
                {
                    endFrameIndex = i;
                    break;
                }
            }

            // If we couldn't find an appropriate frame, use the last one
            if (endFrameIndex == 0 && framePositions.Count > 1)
            {
                endFrameIndex = framePositions.Count - 1;
            }

            // Calculate the size of the data to copy
            long bytesToCopy = framePositions[endFrameIndex];

            // If the segment is too short, we need to loop the audio
            if (bytesToCopy < estimatedBytes / 2)
            {
                // Create a buffer for the looped audio
                byte[] loopedData = new byte[estimatedBytes];
                int position = 0;

                // Loop the replacement audio until we have enough data
                while (position < estimatedBytes)
                {
                    int bytesThisIteration = Math.Min((int)bytesToCopy, estimatedBytes - position);
                    Array.Copy(replacementData, 0, loopedData, position, bytesThisIteration);
                    position += bytesThisIteration;
                }

                return loopedData;
            }

            // Otherwise, extract the portion we need
            byte[] result = new byte[bytesToCopy];
            Array.Copy(replacementData, 0, result, 0, bytesToCopy);

            return result;
        }

        /// <summary>
        /// Finds all MP3 frame sync positions in a byte array
        /// </summary>
        private List<long> FindAllFrameSyncPositions(byte[] data)
        {
            List<long> positions = new List<long>();

            // Skip any ID3 header that might be present
            int startPos = HasId3Header(data) ? GetId3HeaderSize(data) : 0;

            // Find all frame sync markers
            for (long i = startPos; i < data.Length - 1; i++)
            {
                if (data[i] == 0xFF && (data[i + 1] & 0xE0) == 0xE0)
                {
                    positions.Add(i);

                    // Skip ahead since MP3 frames are at least 24 bytes
                    i += 24;
                }
            }

            return positions;
        }

        // Helper method to parse timestamps from format [HH:MM:SS.mmm]
        private bool TryParseTimestamp(string timestamp, out TimeSpan result)
        {
            result = TimeSpan.Zero;

            if (string.IsNullOrEmpty(timestamp))
                return false;

            // Remove brackets if present
            timestamp = timestamp.Trim('[', ']');

            // Try to parse as TimeSpan
            return TimeSpan.TryParse(timestamp, out result);
        }

        /// <summary>
        /// Represents a segment in the MP3 file
        /// </summary>
        private class Mp3Segment
        {
            public long StartPosition { get; set; }
            public long EndPosition { get; set; }
            public double DurationSeconds { get; set; }
            public string Name { get; set; }
        }

        /// <summary>
        /// Contains basic properties of an MP3 file
        /// </summary>
        private class Mp3FileProperties
        {
            public double DurationSeconds { get; set; }
            public int BitrateKbps { get; set; }
            public int SampleRate { get; set; }
            public int Channels { get; set; }
        }
    }
}
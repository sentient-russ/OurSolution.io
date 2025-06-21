using System.Diagnostics;
using NAudio.Wave;
using NAudio.Lame;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;

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
        // Add a new method to extract names without processing them
        public async Task<(List<CutSpeakerModel> Names, int SpeakerId, string SpeakerName)> GetNamesForSelectionAsync(int speakerId)
        {
            var speaker = _dbConnectionService.GetSpeakerById(speakerId);
            string transcription = "";
            if (speaker != null)
            {
                // Add the force parameter to ensure it uses the latest implementation
                transcription = await _transcriptionService.TranscribeSpeakerMp3(speaker, force: true);
                // add line that can be used to load the transcription from a file named 
                Debug.WriteLine($"Transcribed using {_transcriptionService.GetType().Name}");
            }

            using var httpClient = new HttpClient();
            var extractor = new SpeakerExtractor(httpClient);

            // Extract the filename from the transcription string
            string fileName = string.Empty;
            if (transcription.StartsWith("# Transcription of "))
            {
                // Find the text between "# Transcription of " and the next newline or .mp3
                int startIndex = "# Transcription of ".Length;
                int endIndex = transcription.IndexOf("\r\n", startIndex);

                if (endIndex > startIndex)
                {
                    // Extract the full filename (including .mp3 extension)
                    string fullFileName = transcription.Substring(startIndex, endIndex - startIndex);

                    // Store the extracted filename
                    fileName = fullFileName;
                    fileName = Path.GetFileNameWithoutExtension(fullFileName);
                    fileName += "_transcript.txt";
                }
            }

            // Load the transcript from memory
            // fileName = "Adam_R_1750468671654_transcript.txt";
            string fullTranscriptText = await LoadTranscriptionFile(fileName);


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
                        "Henrietta",
                        "AA",
                        "Alcoholics Anonymous",
                        ""
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

            string speakerName = $"{speaker?.FirstName} {speaker?.LastName}";
            return (sanitizedNames, speakerId, speakerName);
        }
        public async Task<string> ReplaceAudioAsync(int speakerId, List<CutSpeakerModel> selectedNames = null, double adjustStartsBy = -.25, double adjustEndsBy = 0.5)
        {
            try
            {
                var speaker = _dbConnectionService.GetSpeakerById(speakerId);
                if (speaker == null)
                {
                    Debug.WriteLine($"Speaker with ID {speakerId} not found");
                    return string.Empty;
                }

                // Use names exactly as provided (already filtered in controller)
                List<CutSpeakerModel> sanitizedNames = selectedNames?.Any() == true 
                    ? selectedNames 
                    : (await GetNamesForSelectionAsync(speakerId)).Names;
                
                Debug.WriteLine($"Processing {sanitizedNames.Count} names {(selectedNames?.Any() == true ? "selected by user" : "automatically")}");

                if (!sanitizedNames.Any()) 
                {
                    Debug.WriteLine("No names found to process");
                    return string.Empty;
                }

                // File paths
                string originalMp3Path = Path.Combine("wwwroot", "uploads", speaker.SecretFileName);
                string replacementAudioPath = Path.Combine("wwwroot", "sounds", "replacement_audio.mp3");
                string outputMp3Path = Path.Combine("wwwroot", "uploads", $"anonymized_{speaker.SecretFileName}");
                
                // Validate input files
                if (!File.Exists(originalMp3Path) || !File.Exists(replacementAudioPath))
                {
                    Debug.WriteLine(!File.Exists(originalMp3Path) 
                        ? $"Original audio file not found: {originalMp3Path}" 
                        : $"Replacement audio file not found: {replacementAudioPath}");
                    return string.Empty;
                }

                // Sort names by start time to ensure proper sequential processing
                sanitizedNames = sanitizedNames.OrderBy(n => 
                {
                    TryParseTimestamp(n.Start, out TimeSpan ts);
                    return ts;
                }).ToList();

                // Store all audio segments
                List<string> audioSegments = new List<string>();
                TimeSpan currentPosition = TimeSpan.Zero;
                string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);

                try
                {
                    // Process each segment between names
                    for (int i = 0; i <= sanitizedNames.Count; i++)
                    {
                        // Determine segment boundaries
                        TimeSpan startPos = currentPosition;
                        TimeSpan? endPos = null;

                        if (i < sanitizedNames.Count)
                        {
                            // Adjust the start timestamp
                            if (!TryParseTimestamp(sanitizedNames[i].Start, out TimeSpan tempEndPos))
                            {
                                Debug.WriteLine($"Failed to parse start timestamp: {sanitizedNames[i].Start}");
                                continue;
                            }
                            endPos = tempEndPos.Add(TimeSpan.FromSeconds(adjustStartsBy)); // Adjust start timestamp
                        }

                        // Extract audio segment before the name (or to the end of file)
                        string segmentPath = Path.Combine(tempDir, $"segment_{i}.mp3");
                        bool segmentSuccess = await ExtractAudioSegment(originalMp3Path, segmentPath, startPos, endPos);
                        
                        if (segmentSuccess)
                        {
                            audioSegments.Add(segmentPath);
                            Debug.WriteLine($"Created segment {i}: {startPos} to {endPos?.ToString() ?? "end"}");
                        }
                        else
                        {
                            Debug.WriteLine($"Failed to create segment {i}");
                            if (audioSegments.Count == 0)
                            {
                                return string.Empty; // No segments created, can't continue
                            }
                        }
                        
                        // If this is a name, add the replacement audio and update position
                        if (i < sanitizedNames.Count)
                        {
                            // Add replacement audio for the name
                            string replacementSegmentPath = Path.Combine(tempDir, $"replacement_{i}.mp3");
                            File.Copy(replacementAudioPath, replacementSegmentPath, true);
                            audioSegments.Add(replacementSegmentPath);
                            
                            // Adjust the end timestamp
                            if (!TryParseTimestamp(sanitizedNames[i].End, out TimeSpan tempCurrentPos))
                            {
                                Debug.WriteLine($"Failed to parse end timestamp: {sanitizedNames[i].End}");
                                continue;
                            }
                            currentPosition = tempCurrentPos.Add(TimeSpan.FromSeconds(adjustEndsBy)); // Adjust end timestamp
                        }
                    }
                    
                    // Combine all segments into the final output
                    Debug.WriteLine($"Combining {audioSegments.Count} segments into final output...");
                    bool combineSuccess = await CombineAudioParts(
                        audioSegments.ToArray(), 
                        outputMp3Path, 
                        GetMp3Properties(originalMp3Path).BitrateKbps);
                        
                    Debug.WriteLine(combineSuccess 
                        ? $"Successfully created combined audio file: {outputMp3Path}" 
                        : "Failed to combine audio segments");
                    
                    return combineSuccess ? outputMp3Path : (audioSegments.Count > 0 ? audioSegments[0] : string.Empty);
                }
                finally
                {
                    // Clean up temp directory and files
                    try
                    {
                        if (Directory.Exists(tempDir))
                        {
                            Directory.Delete(tempDir, true);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Warning: Could not delete temporary directory: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during audio replacement: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Extracts a segment of audio from an MP3 file
        /// </summary>
        private async Task<bool> ExtractAudioSegment(string sourceMp3Path, string outputMp3Path, TimeSpan startTime, TimeSpan? endTime = null)
        {
            string tempWavPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".wav");
            
            try
            {
                using (var reader = new Mp3FileReader(sourceMp3Path))
                {
                    // Calculate bytes to skip to reach start position
                    int bytesToSkip = (int)(reader.WaveFormat.AverageBytesPerSecond * startTime.TotalSeconds);
                    
                    // Create temporary WAV file
                    using (var wavWriter = new WaveFileWriter(tempWavPath, reader.WaveFormat))
                    {
                        // Skip to start position
                        long totalBytesSkipped = 0;
                        byte[] skipBuffer = new byte[4096];
                        int bytesRead;
                        
                        // Skip until we reach the start time
                        while (totalBytesSkipped < bytesToSkip && 
                              (bytesRead = reader.Read(skipBuffer, 0, skipBuffer.Length)) > 0)
                        {
                            totalBytesSkipped += bytesRead;
                            
                            // If we've overshot, write the portion that belongs after the skip point
                            if (totalBytesSkipped > bytesToSkip)
                            {
                                int overshoot = (int)(totalBytesSkipped - bytesToSkip);
                                if (overshoot < bytesRead)
                                {
                                    wavWriter.Write(skipBuffer, bytesRead - overshoot, overshoot);
                                }
                            }
                        }
                        
                        // Read until the end of file or end time
                        byte[] buffer = new byte[4096];
                        long totalBytesRead = 0;
                        long bytesToRead = endTime.HasValue 
                            ? (long)(reader.WaveFormat.AverageBytesPerSecond * (endTime.Value - startTime).TotalSeconds)
                            : long.MaxValue;
                        
                        while ((bytesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            if (endTime.HasValue && totalBytesRead + bytesRead > bytesToRead)
                            {
                                // Only write up to the end timestamp
                                int bytesToWrite = (int)(bytesToRead - totalBytesRead);
                                if (bytesToWrite > 0)
                                {
                                    wavWriter.Write(buffer, 0, bytesToWrite);
                                }
                                break;
                            }
                            
                            wavWriter.Write(buffer, 0, bytesRead);
                            totalBytesRead += bytesRead;
                            
                            if (endTime.HasValue && totalBytesRead >= bytesToRead)
                            {
                                break;
                            }
                        }
                    }
                    
                    // Convert WAV to MP3
                    using (var wavReader = new WaveFileReader(tempWavPath))
                    using (var mp3Writer = new LameMP3FileWriter(outputMp3Path, wavReader.WaveFormat, GetMp3Properties(sourceMp3Path).BitrateKbps))
                    {
                        wavReader.CopyTo(mp3Writer);
                    }
                }
                
                Debug.WriteLine($"Created audio file: {outputMp3Path}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error extracting audio segment: {ex.Message}");
                return false;
            }
            finally
            {
                // Clean up temporary file
                if (File.Exists(tempWavPath))
                {
                    try { File.Delete(tempWavPath); }
                    catch (IOException ex) { Debug.WriteLine($"Warning: Could not delete temporary file {tempWavPath}: {ex.Message}"); }
                }
            }
        }

        /// <summary>
        /// Combines multiple MP3 files into a single file
        /// </summary>
        private async Task<bool> CombineAudioParts(string[] partPaths, string outputPath, int bitrateKbps)
        {
            string tempCombinedWavPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".wav");
            
            try
            {
                // Check if all parts exist
                foreach (string path in partPaths)
                {
                    if (!File.Exists(path))
                    {
                        Debug.WriteLine($"Part file not found: {path}");
                        return false;
                    }
                }
                
                // Get format from first part
                WaveFormat outputFormat;
                using (var reader = new Mp3FileReader(partPaths[0]))
                {
                    outputFormat = reader.WaveFormat;
                }
                
                // Combine all parts
                using (var waveFileWriter = new WaveFileWriter(tempCombinedWavPath, outputFormat))
                {
                    foreach (string partPath in partPaths)
                    {
                        using (var reader = new Mp3FileReader(partPath))
                        {
                            if (!reader.WaveFormat.Equals(outputFormat))
                            {
                                // Format conversion needed
                                using (var resampler = new MediaFoundationResampler(reader, outputFormat))
                                {
                                    await CopyAudioToWriter(resampler, waveFileWriter);
                                }
                            }
                            else
                            {
                                // Direct copy (same format)
                                await CopyAudioToWriter(reader, waveFileWriter);
                            }
                        }
                    }
                }
                
                // Convert to MP3
                using (var wavReader = new WaveFileReader(tempCombinedWavPath))
                using (var mp3Writer = new LameMP3FileWriter(outputPath, wavReader.WaveFormat, bitrateKbps))
                {
                    await wavReader.CopyToAsync(mp3Writer);
                }
                
                Debug.WriteLine($"Successfully created combined audio file: {outputPath}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error combining audio files: {ex.Message}");
                return false;
            }
            finally
            {
                // Clean up
                if (File.Exists(tempCombinedWavPath))
                {
                    try { File.Delete(tempCombinedWavPath); }
                    catch (IOException ex) { Debug.WriteLine($"Warning: Could not delete temporary combined file: {ex.Message}"); }
                }
            }
        }

        /// <summary>
        /// Helper method to copy audio data between streams
        /// </summary>
        private async Task CopyAudioToWriter(IWaveProvider reader, WaveFileWriter writer)
        {
            byte[] buffer = new byte[4096];
            int bytesRead;
            while ((bytesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                writer.Write(buffer, 0, bytesRead);
            }
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
        /// Contains basic properties of an MP3 file
        /// </summary>
        private class Mp3FileProperties
        {
            public double DurationSeconds { get; set; }
            public int BitrateKbps { get; set; }
            public int SampleRate { get; set; }
            public int Channels { get; set; }
        }

        /// <summary>
        /// Helper method to parse timestamps from format [HH:MM:SS.mmm]
        /// </summary>
        private bool TryParseTimestamp(string timestamp, out TimeSpan result)
        {
            result = TimeSpan.Zero;

            // Remove brackets if present
            string timeText = timestamp.Trim('[', ']');

            // Try parsing the timestamp
            if (TimeSpan.TryParse(timeText, out TimeSpan parsedTime))
            {
                result = parsedTime;
                return true;
            }

            return false;
        }
    }
}
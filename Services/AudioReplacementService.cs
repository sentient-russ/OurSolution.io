using System.Diagnostics;
using NAudio.Wave;
using NAudio.Lame;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

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
                // Check if the audio file needs size reduction
                string originalMp3Path = Path.Combine("wwwroot", "uploads", speaker.SecretFileName);
                
                // Reduce file size if needed (max 25MB)
                string processedFilePath = await ReduceAudioFileSize(originalMp3Path);
                
                // If file was reduced, temporarily update the speaker's file reference
                string originalFileName = speaker.SecretFileName;
                bool wasReduced = processedFilePath != originalMp3Path;
                
                if (wasReduced)
                {
                    Debug.WriteLine($"Audio file was reduced in size for transcription");
                    // Update speaker with reduced file path (just for transcription)
                    speaker.SecretFileName = Path.GetFileName(processedFilePath);
                }
                
                try
                {
                    // Perform transcription with the potentially reduced file
                    transcription = await _transcriptionService.TranscribeSpeakerMp3(speaker, force: true);
                    Debug.WriteLine($"Transcribed using {_transcriptionService.GetType().Name}");
                }
                finally
                {
                    // Restore the original file reference if we changed it
                    if (wasReduced)
                    {
                        speaker.SecretFileName = originalFileName;
                        
                        // Clean up the temporary reduced file if it's in the uploads folder
                        if (processedFilePath.Contains("_reduced") && File.Exists(processedFilePath))
                        {
                            try
                            {
                                // Only delete if it's not the original file
                                if (processedFilePath != originalMp3Path)
                                {
                                    File.Delete(processedFilePath);
                                    Debug.WriteLine("Deleted temporary reduced audio file");
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Warning: Could not delete temporary reduced file: {ex.Message}");
                            }
                        }
                    }
                }
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
                    int maxChars = Math.Min(maxTokens * 4, transcriptContext.Length);

                    // Look for a segment break to trim at for cleaner cutting
                    int lastSegmentBreakPos = transcriptContext.LastIndexOf("[", Math.Min(maxChars, transcriptContext.Length));

                    // Choose the best position to trim at (ensuring it doesn't exceed string length)
                    int trimPosition = (lastSegmentBreakPos > 0) ? lastSegmentBreakPos : maxChars;

                    // Final safety check to ensure we never exceed string length
                    trimPosition = Math.Min(trimPosition, transcriptContext.Length);

                    // Trim the text
                    transcriptContext = transcriptContext.Substring(0, trimPosition);
                    Debug.WriteLine($"Trimmed transcript to approximately {trimPosition / 4} tokens");
                }
            }
            else
            {
                // If "## Segments" is not found, use a shorter portion of the transcript
                transcriptContext = fullTranscriptText.Substring(0, Math.Min(fullTranscriptText.Length, 6000)); // 48000 = 12K tokens
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

        public async Task<string> ReplaceAudioAsync(int speakerId, List<CutSpeakerModel> selectedNames = null, double adjustStartsBy = 0, double adjustEndsBy = 0)
        {
            try
            {
                var speaker = _dbConnectionService.GetSpeakerById(speakerId);
                if (speaker == null)
                {
                    Debug.WriteLine($"Speaker with ID {speakerId} not found");
                    return string.Empty;
                }

                // FIXED: Use only the selected names, don't fetch all names if selectedNames is empty
                List<CutSpeakerModel> sanitizedNames = selectedNames ?? new List<CutSpeakerModel>();
                
                Debug.WriteLine($"Processing {sanitizedNames.Count} names selected by user");

                if (!sanitizedNames.Any()) 
                {
                    Debug.WriteLine("No names found to process");
                    return string.Empty;
                }

                // File paths
                string originalMp3Path = Path.Combine("wwwroot", "uploads", speaker.SecretFileName);
                string replacementAudioPath = Path.Combine("wwwroot", "sounds", "replacement_audio.mp3");
                string outputMp3Path = Path.Combine("wwwroot", "uploads", $"anonymized_{speaker.SecretFileName}");
                
                // Create a debug folder to save segments for inspection
                string segmentsFolder = Path.Combine("wwwroot", "uploads", "segments", speakerId.ToString());
                
                // FIXED: Ensure directory exists with proper permissions
                if (!Directory.Exists(segmentsFolder))
                {
                    Directory.CreateDirectory(segmentsFolder);
                }
                
                // Validate input files
                if (!File.Exists(originalMp3Path) || !File.Exists(replacementAudioPath))
                {
                    Debug.WriteLine(!File.Exists(originalMp3Path) 
                        ? $"Original audio file not found: {originalMp3Path}" 
                        : $"Replacement audio file not found: {replacementAudioPath}");
                    return string.Empty;
                }

                // Analyze the replacement audio file to get its volume level
                float replacementVolumeLevel = GetAudioVolumeLevel(replacementAudioPath);
                Debug.WriteLine($"Replacement audio volume level: {replacementVolumeLevel}");
                
                // Analyze the original audio to get its volume level
                float originalVolumeLevel = GetAudioVolumeLevel(originalMp3Path);
                Debug.WriteLine($"Original audio volume level: {originalVolumeLevel}");
                
                // Calculate volume adjustment factor (how much to amplify or reduce)
                float volumeAdjustmentFactor = replacementVolumeLevel > 0 && originalVolumeLevel > 0 
                    ? replacementVolumeLevel / originalVolumeLevel 
                    : 1.0f;
                
                Debug.WriteLine($"Volume adjustment factor: {volumeAdjustmentFactor}");

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
                    // FIXED: Create a metadata file with information about which names are being processed
                    string metadataPath = Path.Combine(segmentsFolder, "processing_metadata.txt");
                    StringBuilder metadata = new StringBuilder();
                    metadata.AppendLine($"Processing started: {DateTime.Now}");
                    metadata.AppendLine($"Speaker ID: {speakerId}");
                    metadata.AppendLine($"Number of names to process: {sanitizedNames.Count}");
                    metadata.AppendLine("Names being processed:");
                    foreach (var name in sanitizedNames)
                    {
                        metadata.AppendLine($"  - {name.Name} (Start: {name.Start}, End: {name.End})");
                    }
                    await File.WriteAllTextAsync(metadataPath, metadata.ToString());
                    
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

                            // Parse end timestamp first to validate
                            if (!TryParseTimestamp(sanitizedNames[i].End, out TimeSpan tempEndTimeRaw))
                            {
                                Debug.WriteLine($"Failed to parse end timestamp: {sanitizedNames[i].End}");
                                continue;
                            }

                            // Validate that end is after start in the original timestamps
                            if (tempEndTimeRaw < tempEndPos)
                            {
                                Debug.WriteLine($"Warning: End time {sanitizedNames[i].End} is before start time {sanitizedNames[i].Start} for name {sanitizedNames[i].Name}");
                                Debug.WriteLine($"Swapping timestamps to ensure valid segment");
                                var temp = tempEndPos;
                                tempEndPos = tempEndTimeRaw;
                                tempEndTimeRaw = temp;
                            }

                            // Calculate adjusted position with safety check for negative values
                            TimeSpan adjustedEndPos = tempEndPos.Add(TimeSpan.FromSeconds(adjustStartsBy));

                            // Ensure we never have a negative timestamp or one before our current position
                            endPos = adjustedEndPos < TimeSpan.Zero ? TimeSpan.Zero : adjustedEndPos;

                            // Additional safety check to ensure endPos is after startPos
                            if (endPos < startPos)
                            {
                                endPos = startPos;
                                Debug.WriteLine($"Adjusted endPos to match startPos to avoid negative duration for name {sanitizedNames[i].Name}");
                            }
                        }

                        // Extract audio segment before the name (or to the end of file)
                        string segmentPath = Path.Combine(tempDir, $"segment_{i}.mp3");
                        bool segmentSuccess = await ExtractAudioSegment(originalMp3Path, segmentPath, startPos, endPos, volumeAdjustmentFactor);
                        
                        if (segmentSuccess)
                        {
                            // FIXED: Make sure we copy with proper permissions and handle any errors
                            try
                            {
                                // Save a copy to the segments folder for inspection
                                string savedSegmentPath = Path.Combine(segmentsFolder, $"segment_{i}.mp3");
                                File.Copy(segmentPath, savedSegmentPath, true);
                                
                                audioSegments.Add(segmentPath);
                                Debug.WriteLine($"Created segment {i}: {startPos} to {endPos?.ToString() ?? "end"}, saved to {savedSegmentPath}");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error saving segment copy: {ex.Message}");
                                // Continue with the process even if saving the copy fails
                                audioSegments.Add(segmentPath);
                            }
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
                            
                            try
                            {
                                // Also save a copy to the segments folder
                                string savedReplacementPath = Path.Combine(segmentsFolder, $"replacement_{i}.mp3");
                                File.Copy(replacementAudioPath, savedReplacementPath, true);
                                
                                Debug.WriteLine($"Added replacement audio at position {i}, saved to {savedReplacementPath}");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Error saving replacement copy: {ex.Message}");
                            }
                            
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
                    
                    // Save the combined WAV file for inspection
                    string combinedWavPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".wav");
                    if (File.Exists(combinedWavPath))
                    {
                        try
                        {
                            string savedCombinedWavPath = Path.Combine(segmentsFolder, "combined_output.wav");
                            File.Copy(combinedWavPath, savedCombinedWavPath, true);
                            Debug.WriteLine($"Saved combined WAV file to {savedCombinedWavPath}");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error saving combined WAV: {ex.Message}");
                        }
                    }
                        
                    Debug.WriteLine(combineSuccess 
                        ? $"Successfully created combined audio file: {outputMp3Path}" 
                        : "Failed to combine audio segments");
                    
                    // Create an info file with details about the processing
                    string infoFilePath = Path.Combine(segmentsFolder, "processing_info.txt");
                    StringBuilder info = new StringBuilder();
                    info.AppendLine($"Processing Date: {DateTime.Now}");
                    info.AppendLine($"Speaker ID: {speakerId}");
                    info.AppendLine($"Original File: {speaker.SecretFileName}");
                    info.AppendLine($"Output File: {Path.GetFileName(outputMp3Path)}");
                    info.AppendLine($"Number of Segments: {audioSegments.Count}");
                    info.AppendLine($"Number of Names Processed: {sanitizedNames.Count}");
                    info.AppendLine($"Volume Adjustment Factor: {volumeAdjustmentFactor}");
                    info.AppendLine($"Names Processed:");
                    
                    for (int i = 0; i < sanitizedNames.Count; i++)
                    {
                        var name = sanitizedNames[i];
                        info.AppendLine($"  {i+1}. Name: {name.Name}, Start: {name.Start}, End: {name.End}");
                    }
                    
                    try
                    {
                        await File.WriteAllTextAsync(infoFilePath, info.ToString());
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error writing info file: {ex.Message}");
                    }
                    
                    return combineSuccess ? outputMp3Path : (audioSegments.Count > 0 ? audioSegments[0] : string.Empty);
                }
                finally
                {
                    // Only clean up the temp directory, not the saved segments in wwwroot
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
        /// Gets the average volume level of an MP3 file
        /// </summary>
        private float GetAudioVolumeLevel(string filePath)
        {
            try
            {
                using (var reader = new Mp3FileReader(filePath))
                {
                    // Convert to a format we can easily analyze
                    using (var waveStream = WaveFormatConversionStream.CreatePcmStream(reader))
                    {
                        // Read the entire file to calculate its average volume
                        byte[] buffer = new byte[waveStream.Length];
                        waveStream.Read(buffer, 0, buffer.Length);

                        // Convert bytes to samples
                        int bytesPerSample = waveStream.WaveFormat.BitsPerSample / 8;
                        int sampleCount = buffer.Length / bytesPerSample;
                        
                        // For simplicity, let's assume 16-bit samples (most common in MP3s)
                        float sum = 0;
                        for (int i = 0; i < buffer.Length; i += 2)
                        {
                            short sample = (short)((buffer[i + 1] << 8) | buffer[i]);
                            float normalizedSample = Math.Abs(sample) / 32768f; // Normalize to 0.0-1.0
                            sum += normalizedSample;
                        }

                        float averageVolume = sum / (sampleCount / waveStream.WaveFormat.Channels);
                        Debug.WriteLine($"Average volume for {Path.GetFileName(filePath)}: {averageVolume}");
                        return averageVolume;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error measuring audio volume: {ex.Message}");
                return 1.0f; // Default to no change
            }
        }

        /// <summary>
        /// Extracts a segment of audio from an MP3 file with optional volume adjustment
        /// </summary>
        private async Task<bool> ExtractAudioSegment(string sourceMp3Path, string outputMp3Path, TimeSpan startTime, TimeSpan? endTime = null, float volumeAdjustmentFactor = 1.0f)
        {
            string tempWavPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".wav");

            try
            {
                // Validate time parameters - ensure end time is after start time
                if (endTime.HasValue && endTime.Value <= startTime)
                {
                    Debug.WriteLine($"Invalid time range: start={startTime}, end={endTime.Value}. End time must be after start time.");

                    // Adjust end time to be 1 second after start time
                    endTime = startTime.Add(TimeSpan.FromSeconds(1));
                    Debug.WriteLine($"Adjusted to valid range: start={startTime}, end={endTime.Value}");
                }

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

                        while (totalBytesSkipped < bytesToSkip &&
                              (bytesRead = reader.Read(skipBuffer, 0, skipBuffer.Length)) > 0)
                        {
                            totalBytesSkipped += bytesRead;
                        }

                        // Read until the end of file or end time
                        byte[] buffer = new byte[4096];
                        long totalBytesRead = 0;
                        long bytesToRead = endTime.HasValue
                            ? (long)(reader.WaveFormat.AverageBytesPerSecond * (endTime.Value - startTime).TotalSeconds)
                            : long.MaxValue;

                        // Ensure we read at least some data (minimum segment size)
                        if (bytesToRead <= 0)
                        {
                            bytesToRead = reader.WaveFormat.AverageBytesPerSecond; // Minimum 1 second of audio
                            Debug.WriteLine($"Setting minimum segment size to {bytesToRead} bytes");
                        }

                        while ((bytesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            if (endTime.HasValue && totalBytesRead + bytesRead > bytesToRead)
                            {
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

                    // Verify the WAV file has content before proceeding
                    var tempWavInfo = new FileInfo(tempWavPath);
                    if (tempWavInfo.Length <= 44) // WAV header is 44 bytes, so this would be an empty audio file
                    {
                        Debug.WriteLine($"Warning: Generated WAV file is empty or contains only a header. Adding 1 second of silence.");
                        
                        // Create a file with 1 second of silence instead
                        using (var silenceWavWriter = new WaveFileWriter(tempWavPath, reader.WaveFormat))
                        {
                            int sampleRate = reader.WaveFormat.SampleRate;
                            int channels = reader.WaveFormat.Channels;
                            int bytesPerSample = reader.WaveFormat.BitsPerSample / 8;
                            
                            // Create 1 second of silence
                            byte[] silence = new byte[sampleRate * channels * bytesPerSample];
                            silenceWavWriter.Write(silence, 0, silence.Length);
                        }
                    }

                    // Convert WAV to MP3 with appropriate bitrate
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
                Debug.WriteLine(ex.StackTrace);
                return false;
            }
            finally
            {
                if (File.Exists(tempWavPath))
                {
                    try { File.Delete(tempWavPath); }
                    catch (IOException ex) { Debug.WriteLine($"Warning: Could not delete temporary file {tempWavPath}: {ex.Message}"); }
                }
            }
        }

        /// <summary>
        /// Adjusts the volume of audio samples in a buffer
        /// </summary>
        private void AdjustVolumeInBuffer(byte[] buffer, int offset, int count, WaveFormat format, float volumeFactor)
        {
            if (format.BitsPerSample != 16)
            {
                // Only supporting 16-bit for simplicity
                Debug.WriteLine($"Volume adjustment only supports 16-bit audio, not {format.BitsPerSample}-bit");
                return;
            }
            
            // Process 16-bit samples
            for (int i = offset; i < offset + count; i += 2)
            {
                if (i + 1 >= buffer.Length) break;
                
                // Convert two bytes to a 16-bit sample
                short sample = (short)((buffer[i + 1] << 8) | buffer[i]);
                
                // Apply volume adjustment
                float adjustedSample = sample * volumeFactor;
                
                // Clip to valid range
                if (adjustedSample > short.MaxValue) adjustedSample = short.MaxValue;
                if (adjustedSample < short.MinValue) adjustedSample = short.MinValue;
                
                // Convert back to bytes
                short adjustedSampleShort = (short)adjustedSample;
                buffer[i] = (byte)(adjustedSampleShort & 0xFF);
                buffer[i + 1] = (byte)((adjustedSampleShort >> 8) & 0xFF);
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
                    //try { File.Delete(tempCombinedWavPath); }
                    //catch (IOException ex) { Debug.WriteLine($"Warning: Could not delete temporary combined file: {ex.Message}"); }
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

        /// <summary>
        /// Reduces the size of an MP3 file to be at or under the specified maximum size in MB
        /// </summary>
        /// <param name="sourceFilePath">Path to the source MP3 file</param>
        /// <param name="maxSizeMB">Maximum size in megabytes (default 25MB)</param>
        /// <returns>Path to the reduced file, or the original path if already under the limit</returns>
        public async Task<string> ReduceAudioFileSize(string sourceFilePath, int maxSizeMB = 25)
        {
            if (!File.Exists(sourceFilePath))
            {
                Debug.WriteLine($"Source file not found: {sourceFilePath}");
                return string.Empty;
            }

            // Check current file size
            var fileInfo = new FileInfo(sourceFilePath);
            long maxSizeBytes = maxSizeMB * 1024 * 1024;
            
            // If file is already smaller than the limit, return the original path
            if (fileInfo.Length <= maxSizeBytes)
            {
                Debug.WriteLine($"File already under size limit ({fileInfo.Length / (1024 * 1024)} MB)");
                return sourceFilePath;
            }
            
            // Create output path for reduced file
            string fileName = Path.GetFileNameWithoutExtension(sourceFilePath);
            string outputFilePath = Path.Combine(
                Path.GetDirectoryName(sourceFilePath),
                $"{fileName}_reduced.mp3");
            
            // Get original audio properties
            var originalProps = GetMp3Properties(sourceFilePath);
            
            // Start with a reasonable bitrate reduction and adjust as needed
            int[] bitratesToTry = { 128, 96, 64, 48, 32 };
            
            foreach (int bitrate in bitratesToTry)
            {
                try
                {
                    // Skip if the current bitrate is already higher than what we're trying
                    if (originalProps.BitrateKbps <= bitrate)
                    {
                        continue;
                    }
                    
                    Debug.WriteLine($"Attempting to reduce file with {bitrate} kbps bitrate");
                    
                    // Decode to WAV first
                    string tempWavPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".wav");
                    
                    try
                    {
                        using (var reader = new Mp3FileReader(sourceFilePath))
                        using (var waveWriter = new WaveFileWriter(tempWavPath, reader.WaveFormat))
                        {
                            byte[] buffer = new byte[4096];
                            int bytesRead;
                            while ((bytesRead = reader.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                waveWriter.Write(buffer, 0, bytesRead);
                            }
                        }
                        
                        // Re-encode to MP3 with lower bitrate
                        using (var reader = new WaveFileReader(tempWavPath))
                        using (var writer = new LameMP3FileWriter(outputFilePath, reader.WaveFormat, bitrate))
                        {
                            await reader.CopyToAsync(writer);
                        }
                        
                        // Check if the new file is under the size limit
                        var reducedFileInfo = new FileInfo(outputFilePath);
                        Debug.WriteLine($"Reduced file size: {reducedFileInfo.Length / (1024 * 1024)} MB");
                        
                        if (reducedFileInfo.Length <= maxSizeBytes)
                        {
                            Debug.WriteLine($"Successfully reduced file to {reducedFileInfo.Length / (1024 * 1024)} MB with {bitrate} kbps bitrate");
                            return outputFilePath;
                        }
                    }
                    finally
                    {
                        if (File.Exists(tempWavPath))
                        {
                            try { File.Delete(tempWavPath); }
                            catch (IOException ex) { Debug.WriteLine($"Warning: Could not delete temp WAV file: {ex.Message}"); }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error while reducing file at {bitrate} kbps: {ex.Message}");
                }
            }
            
            // If we couldn't get under the limit with bitrate reduction alone, try mono conversion
            try
            {
                Debug.WriteLine("Attempting to convert to mono at lowest bitrate");
                
                string tempWavPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".wav");
                
                try
                {
                    using (var reader = new Mp3FileReader(sourceFilePath))
                    {
                        // Create mono format for the output
                        var monoFormat = new WaveFormat(reader.WaveFormat.SampleRate, 1);
                        
                        // Create resampler to convert to mono
                        using (var resampler = new MediaFoundationResampler(reader, monoFormat))
                        using (var waveWriter = new WaveFileWriter(tempWavPath, monoFormat))
                        {
                            byte[] buffer = new byte[4096];
                            int bytesRead;
                            while ((bytesRead = resampler.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                waveWriter.Write(buffer, 0, bytesRead);
                            }
                        }
                        
                        // Re-encode to MP3 with lowest bitrate
                        using (var waveReader = new WaveFileReader(tempWavPath))
                        using (var writer = new LameMP3FileWriter(outputFilePath, waveReader.WaveFormat, 32))
                        {
                            await waveReader.CopyToAsync(writer);
                        }
                        
                        // Check if the new file is under the size limit
                        var reducedFileInfo = new FileInfo(outputFilePath);
                        
                        if (reducedFileInfo.Length <= maxSizeBytes)
                        {
                            Debug.WriteLine($"Successfully reduced file to {reducedFileInfo.Length / (1024 * 1024)} MB with mono conversion");
                            return outputFilePath;
                        }
                        else
                        {
                            Debug.WriteLine($"File still exceeds size limit after all reduction attempts: {reducedFileInfo.Length / (1024 * 1024)} MB");
                            return outputFilePath; // Return anyway, as this is our best effort
                        }
                    }
                }
                finally
                {
                    if (File.Exists(tempWavPath))
                    {
                        try { File.Delete(tempWavPath); }
                        catch (IOException ex) { Debug.WriteLine($"Warning: Could not delete temp WAV file: {ex.Message}"); }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during mono conversion: {ex.Message}");
                return sourceFilePath; // Return original in case of failure
            }
        }
    }
}
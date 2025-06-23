using System.Diagnostics;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using FFMpegCore;
using FFMpegCore.Pipes;
using FFMpegCore.Enums;
using FFMpegCore.Arguments;
using FFMpegCore.Extend;

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

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Transcription file not found: {filePath}");

            return await File.ReadAllTextAsync(filePath);
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

                // Use only the selected names, don't fetch all names if selectedNames is empty
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

                // Ensure directory exists with proper permissions
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
                float replacementVolumeLevel = await GetAudioVolumeLevel(replacementAudioPath);
                Debug.WriteLine($"Replacement audio volume level: {replacementVolumeLevel}");

                // Analyze the original audio to get its volume level
                float originalVolumeLevel = await GetAudioVolumeLevel(originalMp3Path);
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
                    // Create a metadata file with information about which names are being processed
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
                        bool segmentSuccess = await ExtractAudioSegment(originalMp3Path, segmentPath, startPos, endPos);

                        if (segmentSuccess)
                        {
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
                    bool combineSuccess = await CombineAudioParts(audioSegments.ToArray(), outputMp3Path);

                    // Get the temporary combined WAV file path for inspection (though it should be deleted after combination)
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
                        info.AppendLine($"  {i + 1}. Name: {name.Name}, Start: {name.Start}, End: {name.End}");
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
        /// Gets the average volume level of an audio file using FFMpeg
        /// </summary>
        private async Task<float> GetAudioVolumeLevel(string filePath)
        {
            try
            {
                // Run FFMpeg with volumedetect filter to analyze audio volume
                var outputBuilder = new StringBuilder();

                // Create a temporary file path for null output
                string tempOutputPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

                // Build the ffmpeg process correctly for FFMpegCore 5.2.0 using standard arguments
                var ffmpegProcess = FFMpegArguments
                    .FromFileInput(filePath)
                    .OutputToFile(tempOutputPath, false, options =>
                    {
                        // Use WithCustomArgument for both filter and format options
                        options.WithCustomArgument("-af volumedetect");
                        options.WithCustomArgument("-f null");
                    });

                // Capture output
                StringBuilder errorOutput = new StringBuilder();
                bool success = await ffmpegProcess
                    .ProcessAsynchronously();

                // Clean up temporary file if created
                if (File.Exists(tempOutputPath))
                {
                    try { File.Delete(tempOutputPath); } catch { /* Ignore cleanup errors */ }
                }

                string output = errorOutput.ToString();

                // Parse mean_volume from output
                float meanVolume = 0.0f;
                var meanMatch = System.Text.RegularExpressions.Regex.Match(output, @"mean_volume:\s+([-\d.]+)\s+dB");
                if (meanMatch.Success && meanMatch.Groups.Count > 1)
                {
                    if (float.TryParse(meanMatch.Groups[1].Value, out float meanVolumeDb))
                    {
                        // Convert from dB to linear scale (normalized 0.0-1.0)
                        // A volume of 0 dB is maximum, -6 dB is half volume, etc.
                        meanVolume = (float)Math.Pow(10, meanVolumeDb / 20.0);
                    }
                }

                Debug.WriteLine($"Average volume for {Path.GetFileName(filePath)}: {meanVolume}");
                return meanVolume;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error measuring audio volume: {ex.Message}");
                return 1.0f; // Default to no change
            }
        }

        /// <summary>
        /// Extracts a segment of audio from an MP3 file using FFMpeg
        /// </summary>
        private async Task<bool> ExtractAudioSegment(string sourceMp3Path, string outputMp3Path, TimeSpan startTime, TimeSpan? endTime = null)
        {
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

                // Get media info to verify the file exists and has audio
                var mediaInfo = await FFProbe.AnalyseAsync(sourceMp3Path);

                // Check if the segment is empty
                bool emptySegment = false;
                if (endTime.HasValue)
                {
                    double duration = (endTime.Value - startTime).TotalSeconds;
                    if (duration <= 0)
                    {
                        Debug.WriteLine($"Segment has no duration: start={startTime}, end={endTime.Value}");
                        emptySegment = true;
                    }
                }

                if (emptySegment)
                {
                    // Create a 1-second silence segment
                    return await CreateSilenceSegment(outputMp3Path, TimeSpan.FromSeconds(1));
                }
                else
                {
                    // Use FFMpegCore to extract segment
                    var builder = FFMpegArguments.FromFileInput(sourceMp3Path, false, options =>
                    {
                        // Set start time
                        options.Seek(startTime);

                        // Set duration if end time is provided
                        if (endTime.HasValue)
                        {
                            options.WithDuration(endTime.Value - startTime);
                        }
                    });

                    // Set output format and codec options
                    var result = await builder
                        .OutputToFile(outputMp3Path, false, options =>
                        {
                            options.WithAudioCodec(AudioCodec.LibMp3Lame);
                            options.WithAudioBitrate(128); // Default to 128kbps if we can't determine original

                            // Try to maintain the original audio quality
                            if (mediaInfo.PrimaryAudioStream != null)
                            {
                                // Get bitrate 
                                if (mediaInfo.PrimaryAudioStream.BitRate > 0)
                                {
                                    int originalBitrate = (int)(mediaInfo.PrimaryAudioStream.BitRate / 1000);
                                    if (originalBitrate > 0)
                                    {
                                        options.WithAudioBitrate(originalBitrate);
                                    }
                                }

                                // Keep original audio channels and sample rate
                                if (mediaInfo.PrimaryAudioStream.Channels > 0)
                                {
                                    options.WithCustomArgument($"-ac {mediaInfo.PrimaryAudioStream.Channels}");
                                }

                                if (mediaInfo.PrimaryAudioStream.SampleRateHz > 0)
                                {
                                    options.WithCustomArgument($"-ar {mediaInfo.PrimaryAudioStream.SampleRateHz}");
                                }
                            }
                        })
                        .ProcessAsynchronously();

                    // Verify the file was created
                    if (File.Exists(outputMp3Path) && new FileInfo(outputMp3Path).Length > 0)
                    {
                        Debug.WriteLine($"Created audio segment: {outputMp3Path}");
                        return true;
                    }
                    else
                    {
                        Debug.WriteLine($"Failed to create segment or segment is empty: {outputMp3Path}");
                        // If extraction failed, create a short silence segment
                        return await CreateSilenceSegment(outputMp3Path, TimeSpan.FromSeconds(1));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error extracting audio segment: {ex.Message}");
                Debug.WriteLine(ex.StackTrace);

                try
                {
                    // If extraction fails, create a silence segment
                    return await CreateSilenceSegment(outputMp3Path, TimeSpan.FromSeconds(1));
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Creates a silence segment of specified duration
        /// </summary>
        private async Task<bool> CreateSilenceSegment(string outputPath, TimeSpan duration)
        {
            try
            {
                int durationMs = (int)duration.TotalMilliseconds;

                // Use a direct command string approach instead of InputArgument or FromArguments
                string tempScriptPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".txt");
                string ffmpegArgs = $"-f lavfi -i anullsrc=r=44100:cl=stereo -t {duration.TotalSeconds} -c:a libmp3lame -b:a 128k \"{outputPath}\"";

                // Execute the FFmpeg process directly
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = ffmpegArgs,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = new Process { StartInfo = processStartInfo })
                {
                    process.Start();
                    await process.WaitForExitAsync();

                    // Check if the process was successful
                    bool success = process.ExitCode == 0;

                    if (success && File.Exists(outputPath))
                    {
                        Debug.WriteLine($"Created silence segment: {outputPath}");
                        return true;
                    }
                    else
                    {
                        string error = await process.StandardError.ReadToEndAsync();
                        Debug.WriteLine($"FFmpeg error: {error}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating silence segment: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Combines multiple audio files into a single file using FFMpeg
        /// </summary>
        private async Task<bool> CombineAudioParts(string[] partPaths, string outputPath)
        {
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

                // Create a temporary concat file
                string concatListPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".txt");
                using (var writer = new StreamWriter(concatListPath))
                {
                    foreach (string partPath in partPaths)
                    {
                        // Format the path correctly for FFmpeg (escape single quotes and backslashes)
                        string escapedPath = partPath.Replace("\\", "\\\\").Replace("'", "\\'");
                        writer.WriteLine($"file '{escapedPath}'");
                    }
                }

                try
                {
                    // Get media info from the first file to set consistent output parameters
                    var firstFileInfo = await FFProbe.AnalyseAsync(partPaths[0]);

                    // Get the most common properties from all files
                    int bitrate = 128; // Default
                    int sampleRate = 44100; // Default
                    int channels = 2; // Default stereo

                    if (firstFileInfo.PrimaryAudioStream != null)
                    {
                        if (firstFileInfo.PrimaryAudioStream.BitRate > 0)
                        {
                            bitrate = (int)(firstFileInfo.PrimaryAudioStream.BitRate / 1000);
                        }

                        // Extract sample rate directly as int (not doing a boolean comparison)
                        if (firstFileInfo.PrimaryAudioStream.SampleRateHz > 0)
                        {
                            sampleRate = firstFileInfo.PrimaryAudioStream.SampleRateHz;
                        }

                        if (firstFileInfo.PrimaryAudioStream.Channels > 0)
                        {
                            channels = firstFileInfo.PrimaryAudioStream.Channels;
                        }
                    }

                    // Combine files using FFmpeg concat demuxer
                    var result = await FFMpegArguments
                        .FromFileInput(concatListPath, false, options => options.WithCustomArgument("-f concat -safe 0"))
                        .OutputToFile(outputPath, true, options =>
                        {
                            options.WithAudioCodec(AudioCodec.LibMp3Lame);
                            options.WithAudioBitrate(bitrate);
                            options.WithCustomArgument($"-ar {sampleRate} -ac {channels}");
                        })
                        .ProcessAsynchronously();

                    Debug.WriteLine($"Successfully created combined audio file: {outputPath}");
                    return File.Exists(outputPath);
                }
                finally
                {
                    // Clean up the concat file
                    if (File.Exists(concatListPath))
                    {
                        File.Delete(concatListPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error combining audio files: {ex.Message}");
                return false;
            }
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
            var originalProps = await GetMp3Properties(sourceFilePath);

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

                    // Transcode to lower bitrate MP3
                    var result = await FFMpegArguments
                        .FromFileInput(sourceFilePath)
                        .OutputToFile(outputFilePath, true, options =>
                        {
                            options.WithAudioCodec(AudioCodec.LibMp3Lame);
                            options.WithAudioBitrate(bitrate);
                            options.WithCustomArgument("-q:a 5"); // Quality setting for VBR
                        })
                        .ProcessAsynchronously();

                    // Check if the new file is under the size limit
                    var reducedFileInfo = new FileInfo(outputFilePath);
                    Debug.WriteLine($"Reduced file size: {reducedFileInfo.Length / (1024 * 1024)} MB");

                    if (reducedFileInfo.Length <= maxSizeBytes)
                    {
                        Debug.WriteLine($"Successfully reduced file to {reducedFileInfo.Length / (1024 * 1024)} MB with {bitrate} kbps bitrate");
                        return outputFilePath;
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

                // Convert to mono with lowest bitrate
                var result = await FFMpegArguments
                    .FromFileInput(sourceFilePath)
                    .OutputToFile(outputFilePath, true, options =>
                    {
                        options.WithAudioCodec(AudioCodec.LibMp3Lame);
                        options.WithAudioBitrate(32);
                        options.WithCustomArgument("-ac 1"); // Mono
                        options.WithCustomArgument("-q:a 9"); // Low quality setting for maximum compression
                    })
                    .ProcessAsynchronously();

                // Check if the new file is under the size limit
                var reducedFileInfo = new FileInfo(outputFilePath);

                Debug.WriteLine(reducedFileInfo.Length <= maxSizeBytes
                    ? $"Successfully reduced file to {reducedFileInfo.Length / (1024 * 1024)} MB with mono conversion"
                    : $"File still exceeds size limit after all reduction attempts: {reducedFileInfo.Length / (1024 * 1024)} MB");

                return outputFilePath; // Return anyway, as this is our best effort
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during mono conversion: {ex.Message}");
                return sourceFilePath; // Return original in case of failure
            }
        }

        /// <summary>
        /// Gets basic MP3 properties using FFProbe (replaces NAudio functionality)
        /// </summary>
        private async Task<AudioFileProperties> GetMp3Properties(string filePath)
        {
            var result = new AudioFileProperties();

            try
            {
                var mediaInfo = await FFProbe.AnalyseAsync(filePath);

                // Get duration
                result.DurationSeconds = mediaInfo.Duration.TotalSeconds;

                // Get audio properties from primary audio stream
                if (mediaInfo.PrimaryAudioStream != null)
                {
                    // Get bitrate
                    if (mediaInfo.PrimaryAudioStream.BitRate > 0)
                    {
                        result.BitrateKbps = (int)(mediaInfo.PrimaryAudioStream.BitRate / 1000);
                    }
                    else
                    {
                        // Calculate from file size and duration if not explicitly provided
                        if (result.DurationSeconds > 0)
                        {
                            long fileSizeInBits = new FileInfo(filePath).Length * 8;
                            result.BitrateKbps = (int)(fileSizeInBits / result.DurationSeconds / 1000);
                        }
                        else
                        {
                            result.BitrateKbps = 128; // Default
                        }
                    }

                    // Get sample rate
                    if (mediaInfo.PrimaryAudioStream.SampleRateHz > 0)
                    {
                        result.SampleRate = mediaInfo.PrimaryAudioStream.SampleRateHz;
                    }
                    else
                    {
                        result.SampleRate = 44100; // Default
                    }

                    // Get channels
                    if (mediaInfo.PrimaryAudioStream.Channels > 0)
                    {
                        result.Channels = mediaInfo.PrimaryAudioStream.Channels;
                    }
                    else
                    {
                        result.Channels = 2; // Default to stereo
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting MP3 properties: {ex.Message}");
                // Set default values
                result.DurationSeconds = 0;
                result.BitrateKbps = 128;
                result.SampleRate = 44100;
                result.Channels = 2;
            }

            return result;
        }

        /// <summary>
        /// Contains basic properties of an audio file
        /// </summary>
        private class AudioFileProperties
        {
            public double DurationSeconds { get; set; }
            public int BitrateKbps { get; set; }
            public int SampleRate { get; set; }
            public int Channels { get; set; }
        }
    }
}
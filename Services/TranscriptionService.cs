//using Microsoft.Extensions.Logging;
//using Microsoft.Extensions.Configuration;
//using os.Models;
//using System;
//using System.Diagnostics;
//using System.IO;
//using System.Text;
//using System.Text.Json;
//using System.Threading;
//using System.Threading.Tasks;

//namespace os.Services
//{
//    /// <summary>
//    /// Interface for audio transcription services
//    /// </summary>
//    public interface ITranscriptionService
//    {
//        /// <summary>
//        /// Transcribes an MP3 file from a SpeakerModel
//        /// </summary>
//        /// <param name="speaker">The speaker model containing the audio file reference</param>
//        /// <param name="force">If true, forces retranscription even if a transcription already exists</param>
//        /// <param name="cancellationToken">Cancellation token to cancel long-running operation</param>
//        /// <returns>A string containing the transcription with timestamps</returns>
//        Task<string> TranscribeSpeakerMp3(SpeakerModel speaker, bool force = false, CancellationToken cancellationToken = default);

//        /// <summary>
//        /// Transcribes an MP3 file from a file path
//        /// </summary>
//        /// <param name="filePath">Path to the MP3 file</param>
//        /// <param name="cancellationToken">Cancellation token to cancel long-running operation</param>
//        /// <returns>A string containing the transcription with timestamps</returns>
//        Task<string> TranscribeMp3File(string filePath, CancellationToken cancellationToken = default);

//        /// <summary>
//        /// Gets the transcription for a speaker if it exists
//        /// </summary>
//        /// <param name="speaker">The speaker model</param>
//        /// <returns>The transcription text if it exists, null otherwise</returns>
//        Task<string> GetExistingTranscription(SpeakerModel speaker);
//    }

//    /// <summary>
//    /// Service for transcribing audio files to text with timestamps
//    /// </summary>
//    public class TranscriptionService : ITranscriptionService
//    {
//        private readonly ILogger<TranscriptionService> _logger;
//        private readonly IConfiguration _configuration;
//        private readonly string _uploadsFolder;
//        private readonly string _transcriptionsFolder;
//        private readonly string _pythonScriptPath;
//        private readonly TranscriptionStrategy _strategy;
//        private readonly string _modelSize;

//        /// <summary>
//        /// Transcription strategy to use
//        /// </summary>
//        public enum TranscriptionStrategy
//        {
//            OpenAIWhisper,
//            FasterWhisper,
//            WhisperCpp
//        }

//        /// <summary>
//        /// Creates a new transcription service
//        /// </summary>
//        /// <param name="logger">Logger for the transcription service</param>
//        /// <param name="configuration">Configuration for the transcription service</param>
//        public TranscriptionService(ILogger<TranscriptionService> logger, IConfiguration configuration)
//        {
//            _logger = logger;
//            _configuration = configuration;

//            // Get configuration
//            _uploadsFolder = Path.Combine("wwwroot", "uploads");
//            _transcriptionsFolder = Path.Combine("wwwroot", "transcriptions");

//            // Get strategy from configuration or default to FasterWhisper
//            string strategyStr = _configuration["Transcription:Strategy"] ?? "FasterWhisper";
//            if (!Enum.TryParse(strategyStr, true, out _strategy))
//            {
//                _strategy = TranscriptionStrategy.FasterWhisper;
//            }

//            // Get model size from configuration or default to "small"
//            _modelSize = _configuration["Transcription:ModelSize"] ?? "small";

//            // Create transcriptions directory if it doesn't exist
//            if (!Directory.Exists(_transcriptionsFolder))
//            {
//                Directory.CreateDirectory(_transcriptionsFolder);
//            }

//            // Set up Python script for transcription
//            _pythonScriptPath = CreatePythonScript();

//            _logger.LogInformation($"TranscriptionService initialized with strategy: {_strategy}, model size: {_modelSize}");
//        }

//        /// <summary>
//        /// Creates the Python script for transcription
//        /// </summary>
//        /// <returns>Path to the created script</returns>
//        private string CreatePythonScript()
//        {
//            string scriptPath;
//            string scriptContent;

//            switch (_strategy)
//            {
//                case TranscriptionStrategy.FasterWhisper:
//                    scriptPath = Path.Combine(Path.GetTempPath(), "faster_whisper_script.py");
//                    scriptContent = @"
//import sys
//import json
//import os
//from faster_whisper import WhisperModel

//def transcribe(audio_path, output_path, model_size='small'):
//    print(f'Transcribing {audio_path} with model size {model_size}')

//    # Check if file exists
//    if not os.path.exists(audio_path):
//        print(f'Error: File {audio_path} does not exist')
//        sys.exit(1)

//    try:
//        # Load the model - use CPU with int8 quantization for best compatibility
//        model = WhisperModel(model_size, device='cpu', compute_type='int8')

//        # Transcribe with word timestamps
//        segments, info = model.transcribe(audio_path, word_timestamps=True)

//        # Format results
//        result = {
//            'text': '',
//            'segments': []
//        }

//        for segment in segments:
//            segment_data = {
//                'id': len(result['segments']),
//                'start': segment.start,
//                'end': segment.end,
//                'text': segment.text,
//                'words': []
//            }

//            if hasattr(segment, 'words'):
//                for word in segment.words:
//                    segment_data['words'].append({
//                        'start': word.start,
//                        'end': word.end,
//                        'word': word.word
//                    })

//            result['text'] += segment.text + ' '
//            result['segments'].append(segment_data)

//        # Write to output file
//        with open(output_path, 'w', encoding='utf-8') as f:
//            json.dump(result, f, ensure_ascii=False, indent=2)

//        print(f'Transcription saved to {output_path}')

//    except Exception as e:
//        print(f'Error during transcription: {str(e)}')
//        sys.exit(1)

//if __name__ == '__main__':
//    if len(sys.argv) < 3:
//        print('Usage: python script.py input_file output_file [model_size]')
//        sys.exit(1)

//    input_file = sys.argv[1]
//    output_file = sys.argv[2]
//    model_size = sys.argv[3] if len(sys.argv) > 3 else 'small'

//    transcribe(input_file, output_file, model_size)
//";
//                    break;

//                case TranscriptionStrategy.OpenAIWhisper:
//                    scriptPath = Path.Combine(Path.GetTempPath(), "openai_whisper_script.py");
//                    scriptContent = @"
//import sys
//import json
//import os
//import whisper

//def transcribe(audio_path, output_path, model_size='small'):
//    print(f'Transcribing {audio_path} with model size {model_size}')

//    # Check if file exists
//    if not os.path.exists(audio_path):
//        print(f'Error: File {audio_path} does not exist')
//        sys.exit(1)

//    try:
//        # Load the model
//        model = whisper.load_model(model_size)

//        # Transcribe
//        result = model.transcribe(audio_path, word_timestamps=True)

//        # Write to output file
//        with open(output_path, 'w', encoding='utf-8') as f:
//            json.dump(result, f, ensure_ascii=False, indent=2)

//        print(f'Transcription saved to {output_path}')

//    except Exception as e:
//        print(f'Error during transcription: {str(e)}')
//        sys.exit(1)

//if __name__ == '__main__':
//    if len(sys.argv) < 3:
//        print('Usage: python script.py input_file output_file [model_size]')
//        sys.exit(1)

//    input_file = sys.argv[1]
//    output_file = sys.argv[2]
//    model_size = sys.argv[3] if len(sys.argv) > 3 else 'small'

//    transcribe(input_file, output_file, model_size)
//";
//                    break;

//                case TranscriptionStrategy.WhisperCpp:
//                    scriptPath = Path.Combine(Path.GetTempPath(), "whisper_cpp_script.py");
//                    scriptContent = @"
//import sys
//import json
//import os
//import subprocess

//def transcribe(audio_path, output_path, model_size='small'):
//    print(f'Transcribing {audio_path} with model size {model_size}')

//    # Check if file exists
//    if not os.path.exists(audio_path):
//        print(f'Error: File {audio_path} does not exist')
//        sys.exit(1)

//    # Map model size to Whisper.cpp model file
//    model_map = {
//        'tiny': 'models/ggml-tiny.bin',
//        'base': 'models/ggml-base.bin',
//        'small': 'models/ggml-small.bin',
//        'medium': 'models/ggml-medium.bin',
//        'large': 'models/ggml-large.bin'
//    }

//    model_path = model_map.get(model_size, 'models/ggml-small.bin')

//    try:
//        # Get the directory where the script is located
//        whisper_cpp_dir = os.getenv('WHISPER_CPP_DIR', '.')
//        whisper_exe = os.path.join(whisper_cpp_dir, 'main')

//        if not os.path.exists(whisper_exe):
//            print(f'Error: Whisper.cpp executable not found at {whisper_exe}')
//            print('Set the WHISPER_CPP_DIR environment variable to point to your Whisper.cpp directory')
//            sys.exit(1)

//        # Run Whisper.cpp with JSON output
//        temp_json = output_path + '.temp.json'
//        cmd = [
//            whisper_exe,
//            '-m', model_path,
//            '-f', audio_path,
//            '-otxt', 
//            '-oj',
//            '-of', temp_json
//        ]

//        process = subprocess.run(cmd, capture_output=True, text=True)

//        if process.returncode != 0:
//            print(f'Error running Whisper.cpp: {process.stderr}')
//            sys.exit(1)

//        # Read the output and format to match our expected JSON structure
//        if os.path.exists(temp_json):
//            with open(temp_json, 'r', encoding='utf-8') as f:
//                cpp_result = json.load(f)

//            # Convert to our format
//            result = {
//                'text': '',
//                'segments': []
//            }

//            for segment in cpp_result.get('transcription', []):
//                segment_data = {
//                    'id': segment.get('id', 0),
//                    'start': segment.get('from', 0),
//                    'end': segment.get('to', 0),
//                    'text': segment.get('text', ''),
//                    'words': []
//                }

//                # Add words if available
//                for token in segment.get('tokens', []):
//                    if 'from' in token and 'to' in token and 'text' in token:
//                        segment_data['words'].append({
//                            'start': token['from'],
//                            'end': token['to'],
//                            'word': token['text']
//                        })

//                result['text'] += segment_data['text'] + ' '
//                result['segments'].append(segment_data)

//            # Write our formatted result
//            with open(output_path, 'w', encoding='utf-8') as f:
//                json.dump(result, f, ensure_ascii=False, indent=2)

//            # Clean up temp file
//            os.remove(temp_json)

//            print(f'Transcription saved to {output_path}')
//        else:
//            print(f'Error: Whisper.cpp did not produce output file {temp_json}')
//            sys.exit(1)

//    except Exception as e:
//        print(f'Error during transcription: {str(e)}')
//        sys.exit(1)

//if __name__ == '__main__':
//    if len(sys.argv) < 3:
//        print('Usage: python script.py input_file output_file [model_size]')
//        sys.exit(1)

//    input_file = sys.argv[1]
//    output_file = sys.argv[2]
//    model_size = sys.argv[3] if len(sys.argv) > 3 else 'small'

//    transcribe(input_file, output_file, model_size)
//";
//                    break;

//                default:
//                    throw new NotSupportedException($"Transcription strategy {_strategy} is not supported");
//            }

//            // Create the script file
//            File.WriteAllText(scriptPath, scriptContent);
//            return scriptPath;
//        }

//        /// <summary>
//        /// Transcribes an MP3 file from a SpeakerModel
//        /// </summary>
//        public async Task<string> TranscribeSpeakerMp3(SpeakerModel speaker, bool force = false, CancellationToken cancellationToken = default)
//        {
//            if (speaker == null)
//                throw new ArgumentNullException(nameof(speaker));

//            if (string.IsNullOrEmpty(speaker.SecretFileName))
//                throw new ArgumentException("Speaker has no associated audio file");

//            string mp3Path = Path.Combine(_uploadsFolder, speaker.SecretFileName);
//            if (!File.Exists(mp3Path))
//                throw new FileNotFoundException($"Audio file not found: {mp3Path}");

//            // Generate a transcription filename
//            string transcriptionFilename = Path.GetFileNameWithoutExtension(speaker.SecretFileName) + "_transcript.txt";
//            string transcriptionPath = Path.Combine(_transcriptionsFolder, transcriptionFilename);

//            // Check if we already have a transcription and aren't forcing a retranscription
//            if (!force && File.Exists(transcriptionPath))
//            {
//                _logger.LogInformation($"Using existing transcription for {speaker.SecretFileName}");
//                return await File.ReadAllTextAsync(transcriptionPath, cancellationToken);
//            }

//            // Perform transcription
//            _logger.LogInformation($"Starting transcription of {speaker.SecretFileName}");
//            string transcription = await TranscribeMp3File(mp3Path, cancellationToken);

//            // Save transcription for future use
//            await File.WriteAllTextAsync(transcriptionPath, transcription, cancellationToken);
//            _logger.LogInformation($"Transcription saved to {transcriptionPath}");

//            return transcription;
//        }

//        /// <summary>
//        /// Gets the transcription for a speaker if it exists
//        /// </summary>
//        public async Task<string> GetExistingTranscription(SpeakerModel speaker)
//        {
//            if (speaker == null)
//                throw new ArgumentNullException(nameof(speaker));

//            if (string.IsNullOrEmpty(speaker.SecretFileName))
//                return null;

//            string transcriptionFilename = Path.GetFileNameWithoutExtension(speaker.SecretFileName) + "_transcript.txt";
//            string transcriptionPath = Path.Combine(_transcriptionsFolder, transcriptionFilename);

//            if (File.Exists(transcriptionPath))
//            {
//                return await File.ReadAllTextAsync(transcriptionPath);
//            }

//            return null;
//        }

//        /// <summary>
//        /// Transcribes an MP3 file from a file path
//        /// </summary>
//        public async Task<string> TranscribeMp3File(string filePath, CancellationToken cancellationToken = default)
//        {
//            if (string.IsNullOrEmpty(filePath))
//                throw new ArgumentNullException(nameof(filePath));

//            if (!File.Exists(filePath))
//                throw new FileNotFoundException($"File not found: {filePath}");

//            // Create a temporary file for the JSON output
//            string tempOutputFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");

//            try
//            {
//                _logger.LogInformation($"Transcribing {filePath} using {_strategy} strategy with {_modelSize} model");

//                // Configure process for the appropriate transcription strategy
//                ProcessStartInfo startInfo;

//                switch (_strategy)
//                {
//                    case TranscriptionStrategy.FasterWhisper:
//                    case TranscriptionStrategy.OpenAIWhisper:
//                    case TranscriptionStrategy.WhisperCpp:
//                        // All strategies use Python scripts
//                        startInfo = new ProcessStartInfo
//                        {
//                            FileName = "python", // or "python3" depending on your system
//                            Arguments = $"\"{_pythonScriptPath}\" \"{filePath}\" \"{tempOutputFile}\" \"{_modelSize}\"",
//                            RedirectStandardOutput = true,
//                            RedirectStandardError = true,
//                            UseShellExecute = false,
//                            CreateNoWindow = true
//                        };
//                        break;

//                    default:
//                        throw new NotSupportedException($"Transcription strategy {_strategy} is not supported");
//                }

//                using var process = new Process { StartInfo = startInfo };
//                var outputBuilder = new StringBuilder();
//                var errorBuilder = new StringBuilder();

//                // Set up output handlers
//                process.OutputDataReceived += (sender, e) => 
//                {
//                    if (e.Data != null)
//                    {
//                        outputBuilder.AppendLine(e.Data);
//                        _logger.LogDebug($"Transcription process output: {e.Data}");
//                    }
//                };
//                process.ErrorDataReceived += (sender, e) => 
//                {
//                    if (e.Data != null)
//                    {
//                        errorBuilder.AppendLine(e.Data);
//                        _logger.LogWarning($"Transcription process error: {e.Data}");
//                    }
//                };

//                // Start the process and begin reading output
//                process.Start();
//                process.BeginOutputReadLine();
//                process.BeginErrorReadLine();

//                // Create a task to wait for the process to exit
//                var processTask = process.WaitForExitAsync(cancellationToken);

//                // Add a timeout - default to 30 minutes, configurable
//                int timeoutMinutes = int.TryParse(_configuration["Transcription:TimeoutMinutes"], out int configTimeout) 
//                    ? configTimeout 
//                    : 30;

//                var timeoutTask = Task.Delay(TimeSpan.FromMinutes(timeoutMinutes), cancellationToken);

//                // Wait for either the process to complete or the timeout
//                await Task.WhenAny(processTask, timeoutTask);

//                // If the timeout task completed first, cancel the process
//                if (!processTask.IsCompleted)
//                {
//                    try
//                    {
//                        // Try to kill the process
//                        if (!process.HasExited)
//                            process.Kill(true);
//                    }
//                    catch (Exception ex)
//                    {
//                        _logger.LogError(ex, "Error killing transcription process after timeout");
//                    }

//                    throw new TimeoutException($"Transcription process timed out after {timeoutMinutes} minutes");
//                }

//                // Check for errors
//                if (process.ExitCode != 0)
//                {
//                    string errorMessage = errorBuilder.ToString();
//                    _logger.LogError($"Transcription process failed with exit code {process.ExitCode}: {errorMessage}");
//                    throw new Exception($"Transcription failed with exit code {process.ExitCode}: {errorMessage}");
//                }

//                // Check if the output file exists
//                if (!File.Exists(tempOutputFile))
//                {
//                    throw new FileNotFoundException("Transcription failed: Output file not found");
//                }

//                // Parse the JSON output
//                string jsonContent = await File.ReadAllTextAsync(tempOutputFile, cancellationToken);
//                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

//                // Deserialize based on transcription strategy
//                WhisperResult result;
//                try
//                {
//                    result = JsonSerializer.Deserialize<WhisperResult>(jsonContent, options);
//                }
//                catch (JsonException ex)
//                {
//                    _logger.LogError(ex, $"Error deserializing transcription JSON: {jsonContent.Substring(0, Math.Min(200, jsonContent.Length))}...");
//                    throw new FormatException("Failed to parse transcription output", ex);
//                }

//                // Format the output with timestamps
//                var formattedOutput = new StringBuilder();

//                // Add header with file information
//                formattedOutput.AppendLine($"# Transcription of {Path.GetFileName(filePath)}");
//                formattedOutput.AppendLine($"# Created: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
//                formattedOutput.AppendLine($"# Model: {_modelSize}");
//                formattedOutput.AppendLine($"# Strategy: {_strategy}");
//                formattedOutput.AppendLine();

//                // Add full text
//                formattedOutput.AppendLine("## Full Text");
//                formattedOutput.AppendLine(result.Text);
//                formattedOutput.AppendLine();

//                // Add segments with timestamps
//                formattedOutput.AppendLine("## Segments");

//                if (result.Segments != null)
//                {
//                    foreach (var segment in result.Segments)
//                    {
//                        string startTime = FormatTimespan(segment.Start);
//                        string endTime = FormatTimespan(segment.End);
//                        formattedOutput.AppendLine($"[{startTime} ? {endTime}] {segment.Text}");

//                        // Add word-level timestamps if available
//                        if (segment.Words != null && segment.Words.Count > 0)
//                        {
//                            formattedOutput.AppendLine("  Word timestamps:");
//                            foreach (var word in segment.Words)
//                            {
//                                string wordStartTime = FormatTimespan(word.Start);
//                                formattedOutput.AppendLine($"    [{wordStartTime}] {word.Word}");
//                            }
//                            formattedOutput.AppendLine();
//                        }
//                        else
//                        {
//                            formattedOutput.AppendLine();
//                        }
//                    }
//                }

//                _logger.LogInformation($"Transcription of {filePath} completed successfully");
//                return formattedOutput.ToString();
//            }
//            catch (Exception ex) when (!(ex is OperationCanceledException))
//            {
//                _logger.LogError(ex, $"Error transcribing {filePath}");
//                throw;
//            }
//            finally
//            {
//                // Clean up temporary files
//                if (File.Exists(tempOutputFile))
//                {
//                    try
//                    {
//                        File.Delete(tempOutputFile);
//                    }
//                    catch (Exception ex)
//                    {
//                        _logger.LogWarning(ex, $"Failed to delete temporary file {tempOutputFile}");
//                    }
//                }
//            }
//        }

//        /// <summary>
//        /// Formats a timespan in seconds to a readable string
//        /// </summary>
//        private string FormatTimespan(double seconds)
//        {
//            TimeSpan time = TimeSpan.FromSeconds(seconds);
//            return time.ToString(@"hh\:mm\:ss\.fff");
//        }

//        /// <summary>
//        /// Whisper JSON result classes
//        /// </summary>
//        private class WhisperResult
//        {
//            public string Text { get; set; }
//            public List<WhisperSegment> Segments { get; set; }
//        }

//        private class WhisperSegment
//        {
//            public int Id { get; set; }
//            public double Start { get; set; }
//            public double End { get; set; }
//            public string Text { get; set; }
//            public List<WhisperWord> Words { get; set; } = new List<WhisperWord>();
//        }

//        private class WhisperWord
//        {
//            public double Start { get; set; }
//            public double End { get; set; }
//            public string Word { get; set; }
//        }
//    }
//}

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using os.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace os.Services
{
    /// <summary>
    /// Interface for audio transcription services
    /// </summary>
    public interface ITranscriptionService
    {
        /// <summary>
        /// Transcribes an MP3 file from a SpeakerModel
        /// </summary>
        /// <param name="speaker">The speaker model containing the audio file reference</param>
        /// <param name="force">If true, forces retranscription even if a transcription already exists</param>
        /// <param name="cancellationToken">Cancellation token to cancel long-running operation</param>
        /// <returns>A string containing the transcription with timestamps</returns>
        Task<string> TranscribeSpeakerMp3(SpeakerModel speaker, bool force = false, CancellationToken cancellationToken = default);

        /// <summary>
        /// Transcribes an MP3 file from a file path
        /// </summary>
        /// <param name="filePath">Path to the MP3 file</param>
        /// <param name="cancellationToken">Cancellation token to cancel long-running operation</param>
        /// <returns>A string containing the transcription with timestamps</returns>
        Task<string> TranscribeMp3File(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the transcription for a speaker if it exists
        /// </summary>
        /// <param name="speaker">The speaker model</param>
        /// <returns>The transcription text if it exists, null otherwise</returns>
        Task<string> GetExistingTranscription(SpeakerModel speaker);
    }

    /// <summary>
    /// Service for transcribing audio files to text with timestamps
    /// </summary>
    public class TranscriptionService : ITranscriptionService
    {
        private readonly ILogger<TranscriptionService> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _uploadsFolder;
        private readonly string _transcriptionsFolder;
        private readonly string _pythonScriptPath;
        private readonly TranscriptionStrategy _strategy;
        private readonly string _modelSize;
        private readonly bool _useGpu;

        /// <summary>
        /// Transcription strategy to use
        /// </summary>
        public enum TranscriptionStrategy
        {
            OpenAIWhisper,
            FasterWhisper,
            WhisperCpp,
            WhisperS2T
        }

        /// <summary>
        /// Creates a new transcription service
        /// </summary>
        /// <param name="logger">Logger for the transcription service</param>
        /// <param name="configuration">Configuration for the transcription service</param>
        public TranscriptionService(ILogger<TranscriptionService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;

            // Get configuration
            _uploadsFolder = Path.Combine("wwwroot", "uploads");
            _transcriptionsFolder = Path.Combine("wwwroot", "transcriptions");

            // Get strategy from configuration or default to WhisperS2T
            string strategyStr = _configuration["Transcription:Strategy"] ?? "WhisperS2T";
            if (!Enum.TryParse(strategyStr, true, out _strategy))
            {
                _strategy = TranscriptionStrategy.WhisperS2T;
            }

            // Get model size from configuration or default to "medium"
            _modelSize = _configuration["Transcription:ModelSize"] ?? "medium";

            // Determine if GPU should be used (default to true)
            _useGpu = bool.TryParse(_configuration["Transcription:UseGPU"], out bool useGpu) ? useGpu : true;

            // Create transcriptions directory if it doesn't exist
            if (!Directory.Exists(_transcriptionsFolder))
            {
                Directory.CreateDirectory(_transcriptionsFolder);
            }

            // Set up Python script for transcription
            _pythonScriptPath = CreatePythonScript();

            _logger.LogInformation($"TranscriptionService initialized with strategy: {_strategy}, model size: {_modelSize}, GPU: {_useGpu}");
        }

        /// <summary>
        /// Creates the Python script for transcription
        /// </summary>
        /// <returns>Path to the created script</returns>
        private string CreatePythonScript()
        {
            string scriptPath;
            string scriptContent;

            switch (_strategy)
            {
                case TranscriptionStrategy.WhisperS2T:
                    scriptPath = Path.Combine(Path.GetTempPath(), "whisper_s2t_script.py");
                    scriptContent = @"
# Suppress common warnings
import warnings
warnings.filterwarnings('ignore', message='pkg_resources is deprecated as an API')
warnings.filterwarnings('ignore', message='.*Cache-system uses symlinks.*')

import sys
import json
import os
import torch
import numpy as np

def transcribe(audio_path, output_path, model_size='medium', use_gpu=True):
    print(f'Transcribing {audio_path} with WhisperS2T, model size {model_size}, GPU: {use_gpu}')
    
    # Check if file exists
    if not os.path.exists(audio_path):
        print(f'Error: File {audio_path} does not exist')
        sys.exit(1)
        
    try:
        # Dynamically import needed modules
        try:
            from whisper_s2t import WhisperS2T
            from whisper_s2t.utils.vad import VAD
        except ImportError:
            print('WhisperS2T not found, attempting to install...')
            import subprocess
            # Install from GitHub directly
            subprocess.check_call([sys.executable, '-m', 'pip', 'install', 
                                 'git+https://github.com/shashikg/WhisperS2T.git'])
            from whisper_s2t import WhisperS2T
            from whisper_s2t.utils.vad import VAD
            print('WhisperS2T installed successfully')
        
        # Set device based on availability and configuration
        device = 'cuda' if use_gpu and torch.cuda.is_available() else 'cpu'
        if device == 'cuda':
            gpu_info = torch.cuda.get_device_name(0)
            print(f'Using GPU: {gpu_info}')
        else:
            print('Using CPU for transcription')
        
        # Configure compute type based on device
        compute_type = 'float16' if device == 'cuda' else 'int8'
        
        # Initialize VAD for speech detection
        vad = VAD(use_gpu=device=='cuda', vad_onset=0.5, vad_offset=0.5)
        
        # Initialize WhisperS2T model
        model = WhisperS2T(
            model_size=model_size,
            vad=vad,
            device=device,
            compute_type=compute_type,
            cache_dir=os.path.join(os.path.expanduser('~'), '.cache', 'whisper_s2t')
        )
        
        # Configure transcription options
        transcribe_options = {
            'language': 'en',  # Can be configured via parameter if needed
            'task': 'transcribe',
            'word_timestamps': True,
            'condition_on_previous_text': True,
            'vad_filter': True,
            'ts_align': True,
            'temperature': [0.0, 0.2, 0.4, 0.6, 0.8, 1.0],  # Multiple temperatures for better results
            'initial_prompt': None  # Can provide context if needed
        }
        
        # Transcribe with word timestamps
        result = model.transcribe(audio_path, **transcribe_options)
        
        # Format results to match expected format
        formatted_result = {
            'text': result.get('text', ''),
            'segments': []
        }
        
        for i, segment in enumerate(result.get('segments', [])):
            segment_data = {
                'id': i,
                'start': segment.get('start', 0),
                'end': segment.get('end', 0),
                'text': segment.get('text', ''),
                'words': []
            }
            
            # Process word timestamps if available
            if 'words' in segment and segment['words']:
                for word in segment['words']:
                    segment_data['words'].append({
                        'start': word.get('start', 0),
                        'end': word.get('end', 0),
                        'word': word.get('word', '')
                    })
            
            formatted_result['segments'].append(segment_data)
        
        # Write to output file
        with open(output_path, 'w', encoding='utf-8') as f:
            json.dump(formatted_result, f, ensure_ascii=False, indent=2)
        
        print(f'Transcription saved to {output_path}')
        
    except Exception as e:
        print(f'Error during transcription: {str(e)}')
        import traceback
        traceback.print_exc()
        sys.exit(1)

if __name__ == '__main__':
    if len(sys.argv) < 3:
        print('Usage: python script.py input_file output_file [model_size] [use_gpu]')
        sys.exit(1)
    
    input_file = sys.argv[1]
    output_file = sys.argv[2]
    model_size = sys.argv[3] if len(sys.argv) > 3 else 'medium'
    use_gpu = sys.argv[4].lower() == 'true' if len(sys.argv) > 4 else True
    
    transcribe(input_file, output_file, model_size, use_gpu)
";
                    break;

                case TranscriptionStrategy.FasterWhisper:
                    scriptPath = Path.Combine(Path.GetTempPath(), "faster_whisper_script.py");
                    scriptContent = @"
# Suppress common warnings
import warnings
warnings.filterwarnings('ignore', message='pkg_resources is deprecated as an API')
warnings.filterwarnings('ignore', message='.*Cache-system uses symlinks.*')

import sys
import json
import os
from faster_whisper import WhisperModel

def transcribe(audio_path, output_path, model_size='small', use_gpu=False):
    print(f'Transcribing {audio_path} with model size {model_size}')
    
    # Check if file exists
    if not os.path.exists(audio_path):
        print(f'Error: File {audio_path} does not exist')
        sys.exit(1)
        
    try:
        # Load the model - use CPU with int8 quantization for best compatibility
        model = WhisperModel(model_size, device='cpu', compute_type='int8')
        
        # Transcribe with word timestamps
        segments, info = model.transcribe(audio_path, word_timestamps=True)
        
        # Format results
        result = {
            'text': '',
            'segments': []
        }
        
        for segment in segments:
            segment_data = {
                'id': len(result['segments']),
                'start': segment.start,
                'end': segment.end,
                'text': segment.text,
                'words': []
            }
            
            if hasattr(segment, 'words'):
                for word in segment.words:
                    segment_data['words'].append({
                        'start': word.start,
                        'end': word.end,
                        'word': word.word
                    })
            
            result['text'] += segment.text + ' '
            result['segments'].append(segment_data)
        
        # Write to output file
        with open(output_path, 'w', encoding='utf-8') as f:
            json.dump(result, f, ensure_ascii=False, indent=2)
        
        print(f'Transcription saved to {output_path}')
        
    except Exception as e:
        print(f'Error during transcription: {str(e)}')
        import traceback
        traceback.print_exc()
        sys.exit(1)

if __name__ == '__main__':
    if len(sys.argv) < 3:
        print('Usage: python script.py input_file output_file [model_size] [use_gpu]')
        sys.exit(1)
    
    input_file = sys.argv[1]
    output_file = sys.argv[2]
    model_size = sys.argv[3] if len(sys.argv) > 3 else 'small'
    use_gpu = sys.argv[4].lower() == 'true' if len(sys.argv) > 4 else False
    
    transcribe(input_file, output_file, model_size, use_gpu)
";
                    break;

                case TranscriptionStrategy.OpenAIWhisper:
                    scriptPath = Path.Combine(Path.GetTempPath(), "openai_whisper_script.py");
                    scriptContent = @"
# Suppress common warnings
import warnings
warnings.filterwarnings('ignore', message='pkg_resources is deprecated as an API')
warnings.filterwarnings('ignore', message='.*Cache-system uses symlinks.*')

import sys
import json
import os
import whisper

def transcribe(audio_path, output_path, model_size='small', use_gpu=False):
    print(f'Transcribing {audio_path} with model size {model_size}')
    
    # Check if file exists
    if not os.path.exists(audio_path):
        print(f'Error: File {audio_path} does not exist')
        sys.exit(1)
        
    try:
        # Load the model
        model = whisper.load_model(model_size)
        
        # Transcribe
        result = model.transcribe(audio_path, word_timestamps=True)
        
        # Write to output file
        with open(output_path, 'w', encoding='utf-8') as f:
            json.dump(result, f, ensure_ascii=False, indent=2)
        
        print(f'Transcription saved to {output_path}')
        
    except Exception as e:
        print(f'Error during transcription: {str(e)}')
        import traceback
        traceback.print_exc()
        sys.exit(1)

if __name__ == '__main__':
    if len(sys.argv) < 3:
        print('Usage: python script.py input_file output_file [model_size] [use_gpu]')
        sys.exit(1)
    
    input_file = sys.argv[1]
    output_file = sys.argv[2]
    model_size = sys.argv[3] if len(sys.argv) > 3 else 'small'
    use_gpu = sys.argv[4].lower() == 'true' if len(sys.argv) > 4 else False
    
    transcribe(input_file, output_file, model_size, use_gpu)
";
                    break;

                case TranscriptionStrategy.WhisperCpp:
                    scriptPath = Path.Combine(Path.GetTempPath(), "whisper_cpp_script.py");
                    scriptContent = @"
# Suppress common warnings
import warnings
warnings.filterwarnings('ignore', message='pkg_resources is deprecated as an API')
warnings.filterwarnings('ignore', message='.*Cache-system uses symlinks.*')

import sys
import json
import os
import subprocess

def transcribe(audio_path, output_path, model_size='small', use_gpu=False):
    print(f'Transcribing {audio_path} with model size {model_size}, GPU: {use_gpu}')
    
    # Check if file exists
    if not os.path.exists(audio_path):
        print(f'Error: File {audio_path} does not exist')
        sys.exit(1)
    
    # Map model size to Whisper.cpp model file
    model_map = {
        'tiny': 'models/ggml-tiny.bin',
        'base': 'models/ggml-base.bin',
        'small': 'models/ggml-small.bin',
        'medium': 'models/ggml-medium.bin',
        'large': 'models/ggml-large.bin'
    }
    
    model_path = model_map.get(model_size, 'models/ggml-small.bin')
    
    try:
        # Get the directory where the script is located
        whisper_cpp_dir = os.getenv('WHISPER_CPP_DIR', '.')
        whisper_exe = os.path.join(whisper_cpp_dir, 'main')
        
        if not os.path.exists(whisper_exe):
            print(f'Error: Whisper.cpp executable not found at {whisper_exe}')
            print('Set the WHISPER_CPP_DIR environment variable to point to your Whisper.cpp directory')
            sys.exit(1)
        
        # Run Whisper.cpp with JSON output
        temp_json = output_path + '.temp.json'
        cmd = [
            whisper_exe,
            '-m', model_path,
            '-f', audio_path,
            '-otxt', 
            '-oj',
            '-of', temp_json
        ]
        
        # Add GPU parameter if enabled
        if use_gpu:
            cmd.append('-gpu')
        
        process = subprocess.run(cmd, capture_output=True, text=True)
        
        if process.returncode != 0:
            print(f'Error running Whisper.cpp: {process.stderr}')
            sys.exit(1)
        
        # Read the output and format to match our expected JSON structure
        if os.path.exists(temp_json):
            with open(temp_json, 'r', encoding='utf-8') as f:
                cpp_result = json.load(f)
            
            # Convert to our format
            result = {
                'text': '',
                'segments': []
            }
            
            for segment in cpp_result.get('transcription', []):
                segment_data = {
                    'id': segment.get('id', 0),
                    'start': segment.get('from', 0),
                    'end': segment.get('to', 0),
                    'text': segment.get('text', ''),
                    'words': []
                }
                
                # Add words if available
                for token in segment.get('tokens', []):
                    if 'from' in token and 'to' in token and 'text' in token:
                        segment_data['words'].append({
                            'start': token['from'],
                            'end': token['to'],
                            'word': token['text']
                        })
                
                result['text'] += segment_data['text'] + ' '
                result['segments'].append(segment_data)
            
            # Write our formatted result
            with open(output_path, 'w', encoding='utf-8') as f:
                json.dump(result, f, ensure_ascii=False, indent=2)
                
            # Clean up temp file
            os.remove(temp_json)
            
            print(f'Transcription saved to {output_path}')
        else:
            print(f'Error: Whisper.cpp did not produce output file {temp_json}')
            sys.exit(1)
        
    except Exception as e:
        print(f'Error during transcription: {str(e)}')
        import traceback
        traceback.print_exc()
        sys.exit(1)

if __name__ == '__main__':
    if len(sys.argv) < 3:
        print('Usage: python script.py input_file output_file [model_size] [use_gpu]')
        sys.exit(1)
    
    input_file = sys.argv[1]
    output_file = sys.argv[2]
    model_size = sys.argv[3] if len(sys.argv) > 3 else 'small'
    use_gpu = sys.argv[4].lower() == 'true' if len(sys.argv) > 4 else False
    
    transcribe(input_file, output_file, model_size, use_gpu)
";
                    break;

                default:
                    throw new NotSupportedException($"Transcription strategy {_strategy} is not supported");
            }

            // Create the script file
            File.WriteAllText(scriptPath, scriptContent);
            return scriptPath;
        }

        /// <summary>
        /// Transcribes an MP3 file from a SpeakerModel
        /// </summary>
        public async Task<string> TranscribeSpeakerMp3(SpeakerModel speaker, bool force = false, CancellationToken cancellationToken = default)
        {
            if (speaker == null)
                throw new ArgumentNullException(nameof(speaker));

            if (string.IsNullOrEmpty(speaker.SecretFileName))
                throw new ArgumentException("Speaker has no associated audio file");

            string mp3Path = Path.Combine(_uploadsFolder, speaker.SecretFileName);
            if (!File.Exists(mp3Path))
                throw new FileNotFoundException($"Audio file not found: {mp3Path}");

            // Generate a transcription filename
            string transcriptionFilename = Path.GetFileNameWithoutExtension(speaker.SecretFileName) + "_transcript.txt";
            string transcriptionPath = Path.Combine(_transcriptionsFolder, transcriptionFilename);

            // Check if we already have a transcription and aren't forcing a retranscription
            if (!force && File.Exists(transcriptionPath))
            {
                _logger.LogInformation($"Using existing transcription for {speaker.SecretFileName}");
                return await File.ReadAllTextAsync(transcriptionPath, cancellationToken);
            }

            // Perform transcription
            _logger.LogInformation($"Starting transcription of {speaker.SecretFileName}");
            string transcription = await TranscribeMp3File(mp3Path, cancellationToken);

            // Save transcription for future use
            await File.WriteAllTextAsync(transcriptionPath, transcription, cancellationToken);
            _logger.LogInformation($"Transcription saved to {transcriptionPath}");

            return transcription;
        }

        /// <summary>
        /// Gets the transcription for a speaker if it exists
        /// </summary>
        public async Task<string> GetExistingTranscription(SpeakerModel speaker)
        {
            if (speaker == null)
                throw new ArgumentNullException(nameof(speaker));

            if (string.IsNullOrEmpty(speaker.SecretFileName))
                return null;

            string transcriptionFilename = Path.GetFileNameWithoutExtension(speaker.SecretFileName) + "_transcript.txt";
            string transcriptionPath = Path.Combine(_transcriptionsFolder, transcriptionFilename);

            if (File.Exists(transcriptionPath))
            {
                return await File.ReadAllTextAsync(transcriptionPath);
            }

            return null;
        }

        /// <summary>
        /// Transcribes an MP3 file from a file path
        /// </summary>
        public async Task<string> TranscribeMp3File(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");

            // Create a temporary file for the JSON output
            string tempOutputFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");

            try
            {
                _logger.LogInformation($"Transcribing {filePath} using {_strategy} strategy with {_modelSize} model, GPU: {_useGpu}");

                // Configure process for the appropriate transcription strategy
                ProcessStartInfo startInfo;

                // All strategies use Python scripts with the same arguments pattern
                startInfo = new ProcessStartInfo
                {
                    FileName = "python", // or "python3" depending on your system
                    Arguments = $"\"{_pythonScriptPath}\" \"{filePath}\" \"{tempOutputFile}\" \"{_modelSize}\" \"{_useGpu}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                // Add environment variables to suppress warnings
                startInfo.EnvironmentVariables["HF_HUB_DISABLE_SYMLINKS_WARNING"] = "1";
                startInfo.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";

                using var process = new Process { StartInfo = startInfo };
                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                // Set up output handlers
                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        outputBuilder.AppendLine(e.Data);
                        _logger.LogDebug($"Transcription process output: {e.Data}");
                    }
                };
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        errorBuilder.AppendLine(e.Data);
                        _logger.LogWarning($"Transcription process error: {e.Data}");
                    }
                };

                // Start the process and begin reading output
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Create a task to wait for the process to exit
                var processTask = process.WaitForExitAsync(cancellationToken);

                // Add a timeout - default to 30 minutes, configurable
                int timeoutMinutes = int.TryParse(_configuration["Transcription:TimeoutMinutes"], out int configTimeout)
                    ? configTimeout
                    : 30;

                var timeoutTask = Task.Delay(TimeSpan.FromMinutes(timeoutMinutes), cancellationToken);

                // Wait for either the process to complete or the timeout
                await Task.WhenAny(processTask, timeoutTask);

                // If the timeout task completed first, cancel the process
                if (!processTask.IsCompleted)
                {
                    try
                    {
                        // Try to kill the process
                        if (!process.HasExited)
                            process.Kill(true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error killing transcription process after timeout");
                    }

                    throw new TimeoutException($"Transcription process timed out after {timeoutMinutes} minutes");
                }

                // Check for errors
                if (process.ExitCode != 0)
                {
                    string errorMessage = errorBuilder.ToString();
                    _logger.LogError($"Transcription process failed with exit code {process.ExitCode}: {errorMessage}");
                    throw new Exception($"Transcription failed with exit code {process.ExitCode}: {errorMessage}");
                }

                // Check if the output file exists
                if (!File.Exists(tempOutputFile))
                {
                    throw new FileNotFoundException("Transcription failed: Output file not found");
                }

                // Parse the JSON output
                string jsonContent = await File.ReadAllTextAsync(tempOutputFile, cancellationToken);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                // Deserialize based on transcription strategy
                WhisperResult result;
                try
                {
                    result = JsonSerializer.Deserialize<WhisperResult>(jsonContent, options);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, $"Error deserializing transcription JSON: {jsonContent.Substring(0, Math.Min(200, jsonContent.Length))}...");
                    throw new FormatException("Failed to parse transcription output", ex);
                }

                // Format the output with timestamps
                var formattedOutput = new StringBuilder();

                // Add header with file information
                formattedOutput.AppendLine($"# Transcription of {Path.GetFileName(filePath)}");
                formattedOutput.AppendLine($"# Created: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
                formattedOutput.AppendLine($"# Model: {_modelSize}");
                formattedOutput.AppendLine($"# Strategy: {_strategy}");
                formattedOutput.AppendLine($"# GPU: {_useGpu}");
                formattedOutput.AppendLine();

                // Add full text
                formattedOutput.AppendLine("## Full Text");
                formattedOutput.AppendLine(result.Text);
                formattedOutput.AppendLine();

                // Add segments with timestamps
                formattedOutput.AppendLine("## Segments");

                if (result.Segments != null)
                {
                    foreach (var segment in result.Segments)
                    {
                        string startTime = FormatTimespan(segment.Start);
                        string endTime = FormatTimespan(segment.End);
                        formattedOutput.AppendLine($"[{startTime} ? {endTime}] {segment.Text}");

                        // Add word-level timestamps if available
                        if (segment.Words != null && segment.Words.Count > 0)
                        {
                            formattedOutput.AppendLine("  Word timestamps:");
                            foreach (var word in segment.Words)
                            {
                                string wordStartTime = FormatTimespan(word.Start);
                                formattedOutput.AppendLine($"    [{wordStartTime}] {word.Word}");
                            }
                            formattedOutput.AppendLine();
                        }
                        else
                        {
                            formattedOutput.AppendLine();
                        }
                    }
                }

                _logger.LogInformation($"Transcription of {filePath} completed successfully");
                return formattedOutput.ToString();
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _logger.LogError(ex, $"Error transcribing {filePath}");
                throw;
            }
            finally
            {
                // Clean up temporary files
                if (File.Exists(tempOutputFile))
                {
                    try
                    {
                        File.Delete(tempOutputFile);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to delete temporary file {tempOutputFile}");
                    }
                }
            }
        }

        /// <summary>
        /// Formats a timespan in seconds to a readable string
        /// </summary>
        private string FormatTimespan(double seconds)
        {
            TimeSpan time = TimeSpan.FromSeconds(seconds);
            return time.ToString(@"hh\:mm\:ss\.fff");
        }

        /// <summary>
        /// Whisper JSON result classes
        /// </summary>
        private class WhisperResult
        {
            public string Text { get; set; }
            public List<WhisperSegment> Segments { get; set; }
        }

        private class WhisperSegment
        {
            public int Id { get; set; }
            public double Start { get; set; }
            public double End { get; set; }
            public string Text { get; set; }
            public List<WhisperWord> Words { get; set; } = new List<WhisperWord>();
        }

        private class WhisperWord
        {
            public double Start { get; set; }
            public double End { get; set; }
            public string Word { get; set; }
        }
    }
}
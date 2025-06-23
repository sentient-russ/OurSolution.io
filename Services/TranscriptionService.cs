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
using System.Collections.Generic;
using System.Runtime.InteropServices;

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
        private static readonly SemaphoreSlim _gpuLock = new(1, 1); // GPU access lock

        /// <summary>
        /// Transcription strategy to use
        /// </summary>
        public enum TranscriptionStrategy
        {
            OpenAIWhisper,
            FasterWhisper,
            WhisperCpp
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

            _uploadsFolder = Path.Combine("wwwroot", "uploads");
            _transcriptionsFolder = Path.Combine("wwwroot", "transcriptions");

            string strategyStr = _configuration["Transcription:Strategy"] ?? "FasterWhisper";
            if (!Enum.TryParse(strategyStr, true, out _strategy))
            {
                _strategy = TranscriptionStrategy.FasterWhisper;
            }

            _modelSize = _configuration["Transcription:ModelSize"] ?? "small";
            _useGpu = bool.TryParse(_configuration["Transcription:UseGPU"], out bool useGpu) ? useGpu : true;

            if (!Directory.Exists(_transcriptionsFolder))
            {
                Directory.CreateDirectory(_transcriptionsFolder);
            }

            _pythonScriptPath = CreatePythonScript();
            _logger.LogInformation($"TranscriptionService initialized with strategy: {_strategy}, model size: {_modelSize}, GPU: {_useGpu}");
        }

        private string CreatePythonScript()
        {
            string scriptPath = Path.Combine(Path.GetTempPath(), "faster_whisper_script.py");

            // Use the improved Python script from the artifact above
            string scriptContent = @"
import os
os.environ['KMP_DUPLICATE_LIB_OK'] = 'TRUE'
os.environ['HF_HUB_DISABLE_SYMLINKS_WARNING'] = '1'
os.environ['PYTHONIOENCODING'] = 'utf-8'
os.environ['PYTORCH_CUDA_ALLOC_CONF'] = 'max_split_size_mb:128,garbage_collection_threshold:0.6,expandable_segments:True'
os.environ['CUDA_LAUNCH_BLOCKING'] = '1'  # For debugging CUDA issues
os.environ['CUDA_VISIBLE_DEVICES'] = '0'
os.environ['OMP_NUM_THREADS'] = '1'

import warnings
warnings.filterwarnings('ignore', message='pkg_resources is deprecated as an API')
warnings.filterwarnings('ignore', message='.*Cache-system uses symlinks.*')
warnings.filterwarnings('ignore', category=UserWarning)

import sys
import json
import gc
import torch
import tempfile
import time
from pathlib import Path

def check_cuda_availability():
    print('=== CUDA Environment Check ===')
    print(f'PyTorch version: {torch.__version__}')
    print(f'CUDA available: {torch.cuda.is_available()}')
    
    if torch.cuda.is_available():
        print(f'CUDA version: {torch.version.cuda}')
        print(f'cuDNN version: {torch.backends.cudnn.version()}')
        print(f'Number of GPUs: {torch.cuda.device_count()}')
        
        for i in range(torch.cuda.device_count()):
            props = torch.cuda.get_device_properties(i)
            print(f'GPU {i}: {props.name}')
            print(f'  Memory: {props.total_memory / 1024**3:.2f} GB')
            print(f'  Compute capability: {props.major}.{props.minor}')
        
        try:
            torch.cuda.empty_cache()
            free_mem, total_mem = torch.cuda.mem_get_info(0)
            print(f'Current GPU memory: {free_mem / 1024**3:.2f}GB free / {total_mem / 1024**3:.2f}GB total')
        except Exception as e:
            print(f'Error checking GPU memory: {e}')
    else:
        print('CUDA is not available')
    print('=== End CUDA Check ===\n')

def transcribe(audio_path, output_path, model_size='small', use_gpu=False):
    print(f'Transcribing {audio_path} with model size {model_size}, GPU: {use_gpu}')
    
    if not os.path.exists(audio_path):
        print(f'Error: File {audio_path} does not exist')
        sys.exit(1)
    
    check_cuda_availability()
    
    try:
        from faster_whisper import WhisperModel
        from pydub import AudioSegment
        import librosa
        
        if use_gpu and torch.cuda.is_available():
            torch.cuda.empty_cache()
            gc.collect()
        
        device = 'cuda' if use_gpu and torch.cuda.is_available() else 'cpu'
        
        if device == 'cuda':
            compute_type = 'float16'
            try:
                free_mem, total_mem = torch.cuda.mem_get_info(0)
                free_mem_gb = free_mem / (1024**3)
                
                if free_mem_gb < 3.0:
                    print(f'WARNING: Low GPU memory ({free_mem_gb:.2f}GB free). Using more conservative settings.')
                    compute_type = 'int8'
                    
            except Exception as e:
                print(f'Error checking GPU memory, falling back to CPU: {e}')
                device = 'cpu'
                compute_type = 'int8'
        else:
            compute_type = 'int8'
        
        print(f'Using device: {device}, compute_type: {compute_type}')
        
        try:
            duration = librosa.get_duration(path=audio_path)
            print(f'Audio duration: {duration:.2f} seconds')
        except Exception as e:
            print(f'Warning: Could not determine audio duration: {e}')
            duration = 0
        
        try:
            print('Initializing Whisper model...')
            model_kwargs = {
                'model_size_or_path': model_size,
                'device': device,
                'compute_type': compute_type,
                'download_root': 'models',
                'cpu_threads': 2,
            }
            
            if device == 'cuda':
                model_kwargs.update({
                    'num_workers': 1,
                })
            
            model = WhisperModel(**model_kwargs)
            print('Model initialized successfully')
            
        except Exception as e:
            print(f'Error initializing model: {e}')
            if use_gpu:
                print('Falling back to CPU...')
                device = 'cpu'
                compute_type = 'int8'
                model = WhisperModel(
                    model_size_or_path=model_size,
                    device=device,
                    compute_type=compute_type,
                    download_root='models',
                    cpu_threads=2
                )
            else:
                raise
        
        print('Starting transcription...')
        
        transcribe_kwargs = {
            'audio': audio_path,
            'word_timestamps': True,
            'beam_size': 1,
            'vad_filter': True,
            'vad_parameters': {
                'min_silence_duration_ms': 500,
                'max_speech_duration_s': 30,
            },
            'language': None,
            'condition_on_previous_text': False,
        }
        
        if duration > 120 and device == 'cuda':
            print('Using chunked processing for long audio...')
            transcribe_kwargs['chunk_length'] = 30
        
        try:
            segments, info = model.transcribe(**transcribe_kwargs)
            segments = list(segments)
            
        except Exception as e:
            print(f'Error during transcription: {e}')
            if device == 'cuda':
                print('Transcription failed on GPU, trying CPU...')
                del model
                torch.cuda.empty_cache()
                gc.collect()
                
                model = WhisperModel(
                    model_size_or_path=model_size,
                    device='cpu',
                    compute_type='int8',
                    download_root='models',
                    cpu_threads=2
                )
                
                transcribe_kwargs['audio'] = audio_path
                segments, info = model.transcribe(**transcribe_kwargs)
                segments = list(segments)
            else:
                raise
        
        print(f'Transcription completed. Language: {info.language}, probability: {info.language_probability:.2f}')
        
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
            
            if hasattr(segment, 'words') and segment.words:
                for word in segment.words:
                    segment_data['words'].append({
                        'start': word.start,
                        'end': word.end,
                        'word': word.word
                    })
            
            result['text'] += segment.text + ' '
            result['segments'].append(segment_data)
        
        with open(output_path, 'w', encoding='utf-8') as f:
            json.dump(result, f, ensure_ascii=False, indent=2)
        
        print(f'Transcription saved to {output_path}')
        
        del model
        gc.collect()
        if device == 'cuda':
            torch.cuda.empty_cache()
        
    except Exception as e:
        print(f'Fatal error during transcription: {str(e)}')
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

            File.WriteAllText(scriptPath, scriptContent);
            return scriptPath;
        }

        public async Task<string> TranscribeSpeakerMp3(SpeakerModel speaker, bool force = false, CancellationToken cancellationToken = default)
        {
            if (speaker == null)
                throw new ArgumentNullException(nameof(speaker));

            if (string.IsNullOrEmpty(speaker.SecretFileName))
                throw new ArgumentException("Speaker has no associated audio file");

            string mp3Path = Path.Combine(_uploadsFolder, speaker.SecretFileName);
            if (!File.Exists(mp3Path))
                throw new FileNotFoundException($"Audio file not found: {mp3Path}");

            string transcriptionFilename = Path.GetFileNameWithoutExtension(speaker.SecretFileName) + "_transcript.txt";
            string transcriptionPath = Path.Combine(_transcriptionsFolder, transcriptionFilename);

            if (!force && File.Exists(transcriptionPath))
            {
                _logger.LogInformation($"Using existing transcription for {speaker.SecretFileName}");
                return await File.ReadAllTextAsync(transcriptionPath, cancellationToken);
            }

            try
            {
                _logger.LogInformation($"Starting transcription of {speaker.SecretFileName} with GPU: {_useGpu}");
                string transcription = await TranscribeMp3File(mp3Path, cancellationToken);

                await File.WriteAllTextAsync(transcriptionPath, transcription, cancellationToken);
                _logger.LogInformation($"Transcription saved to {transcriptionPath}");
                return transcription;
            }
            catch (Exception ex) when (ex.Message.Contains("-1073740791") && _useGpu)
            {
                _logger.LogWarning($"GPU transcription failed with memory error. Falling back to CPU. Error: {ex.Message}");
                string transcription = await TranscribeMp3FileWithMode(mp3Path, false, cancellationToken);

                await File.WriteAllTextAsync(transcriptionPath, transcription, cancellationToken);
                _logger.LogInformation($"CPU transcription saved to {transcriptionPath}");
                return transcription;
            }
        }

        private async Task<string> TranscribeMp3FileWithMode(string filePath, bool useGpu, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");

            string tempOutputFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");

            try
            {
                if (useGpu)
                {
                    await _gpuLock.WaitAsync(cancellationToken);
                }

                _logger.LogInformation($"Transcribing {filePath} using {_strategy} strategy with {_modelSize} model, GPU: {useGpu}");

                ProcessStartInfo startInfo = new()
                {
                    FileName = "python",
                    Arguments = $"\"{_pythonScriptPath}\" \"{filePath}\" \"{tempOutputFile}\" \"{_modelSize}\" \"{useGpu}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Environment.CurrentDirectory
                };

                // Enhanced environment variables for stability
                var envVars = new Dictionary<string, string>
                {
                    ["CUDA_VISIBLE_DEVICES"] = "0",
                    ["OMP_NUM_THREADS"] = "1",
                    ["PYTORCH_CUDA_ALLOC_CONF"] = "garbage_collection_threshold:0.6,max_split_size_mb:128,expandable_segments:True",
                    ["HF_HUB_DISABLE_SYMLINKS_WARNING"] = "1",
                    ["PYTHONIOENCODING"] = "utf-8",
                    ["KMP_DUPLICATE_LIB_OK"] = "TRUE",
                    ["CUDA_LAUNCH_BLOCKING"] = "1", // For debugging
                    ["PYTHONUNBUFFERED"] = "1", // Ensure real-time output
                };

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    envVars["PYTHONSTACKSIZE"] = "8388608";
                    envVars["CUDA_CACHE_DISABLE"] = "1"; // Disable CUDA cache on Windows
                }

                foreach (var kvp in envVars)
                {
                    startInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
                }

                string pythonPath = _configuration["Transcription:PythonPath"];
                if (!string.IsNullOrEmpty(pythonPath))
                {
                    startInfo.FileName = pythonPath;
                    _logger.LogInformation($"Using custom Python path: {pythonPath}");
                }

                using var process = new Process { StartInfo = startInfo };
                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

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
                        // Log CUDA-related errors as warnings, others as debug
                        if (e.Data.Contains("CUDA", StringComparison.OrdinalIgnoreCase) ||
                            e.Data.Contains("GPU", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogWarning($"GPU-related message: {e.Data}");
                        }
                        else
                        {
                            _logger.LogDebug($"Transcription process stderr: {e.Data}");
                        }
                    }
                };

                _logger.LogInformation("Starting transcription process...");
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                var processTask = process.WaitForExitAsync(cancellationToken);
                int timeoutMinutes = int.TryParse(_configuration["Transcription:TimeoutMinutes"], out int configTimeout)
                    ? configTimeout
                    : 30;

                var timeoutTask = Task.Delay(TimeSpan.FromMinutes(timeoutMinutes), cancellationToken);
                var completedTask = await Task.WhenAny(processTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    _logger.LogError($"Transcription process timed out after {timeoutMinutes} minutes");
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill(true);
                            await Task.Delay(1000, CancellationToken.None); // Give it time to cleanup
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error killing transcription process after timeout");
                    }

                    throw new TimeoutException($"Transcription process timed out after {timeoutMinutes} minutes");
                }

                // Wait a bit more for output to be captured
                await Task.Delay(500, CancellationToken.None);

                string outputText = outputBuilder.ToString();
                string errorText = errorBuilder.ToString();

                _logger.LogInformation($"Transcription process completed with exit code: {process.ExitCode}");

                if (!string.IsNullOrEmpty(outputText))
                {
                    _logger.LogDebug($"Process output: {outputText}");
                }

                if (process.ExitCode != 0)
                {
                    _logger.LogError($"Transcription process failed with exit code {process.ExitCode}");
                    _logger.LogError($"Error output: {errorText}");

                    // Check for specific error patterns
                    if (process.ExitCode == -1073740791 || errorText.Contains("0xC0000409"))
                    {
                        throw new Exception($"GPU/CUDA stack overflow error (exit code {process.ExitCode}). This usually indicates CUDA driver issues, insufficient GPU memory management, or hardware compatibility problems. Error: {errorText}");
                    }

                    throw new Exception($"Transcription failed with exit code {process.ExitCode}: {errorText}");
                }

                if (!File.Exists(tempOutputFile))
                {
                    _logger.LogError($"Output file not found: {tempOutputFile}");
                    _logger.LogError($"Process output: {outputText}");
                    _logger.LogError($"Process errors: {errorText}");
                    throw new FileNotFoundException($"Transcription failed: Output file not found. Process may have crashed.");
                }

                string jsonContent = await File.ReadAllTextAsync(tempOutputFile, cancellationToken);

                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    throw new FormatException("Transcription output file is empty");
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                WhisperResult result;
                try
                {
                    result = JsonSerializer.Deserialize<WhisperResult>(jsonContent, options);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, $"Error deserializing transcription JSON. Content: {jsonContent.Substring(0, Math.Min(500, jsonContent.Length))}...");
                    throw new FormatException("Failed to parse transcription output", ex);
                }

                if (result == null)
                {
                    throw new FormatException("Deserialized transcription result is null");
                }

                // Build formatted output
                var formattedOutput = new StringBuilder();
                formattedOutput.AppendLine($"# Transcription of {Path.GetFileName(filePath)}");
                formattedOutput.AppendLine($"# Created: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
                formattedOutput.AppendLine($"# Model: {_modelSize}");
                formattedOutput.AppendLine($"# Strategy: {_strategy}");
                formattedOutput.AppendLine($"# GPU: {useGpu}");
                formattedOutput.AppendLine();

                formattedOutput.AppendLine("## Full Text");
                formattedOutput.AppendLine(result.Text ?? string.Empty);
                formattedOutput.AppendLine();

                formattedOutput.AppendLine("## Segments");

                if (result.Segments != null)
                {
                    foreach (var segment in result.Segments)
                    {
                        string startTime = FormatTimespan(segment.Start);
                        string endTime = FormatTimespan(segment.End);
                        formattedOutput.AppendLine($"[{startTime} → {endTime}] {segment.Text ?? string.Empty}");

                        if (segment.Words != null && segment.Words.Count > 0)
                        {
                            formattedOutput.AppendLine("  Word timestamps:");
                            foreach (var word in segment.Words)
                            {
                                string wordStartTime = FormatTimespan(word.Start);
                                formattedOutput.AppendLine($"    [{wordStartTime}] {word.Word ?? string.Empty}");
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
                if (useGpu)
                {
                    _gpuLock.Release();
                }

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

        public async Task<string> TranscribeMp3File(string filePath, CancellationToken cancellationToken = default)
        {
            return await TranscribeMp3FileWithMode(filePath, _useGpu, cancellationToken);
        }

        private string FormatTimespan(double seconds)
        {
            TimeSpan time = TimeSpan.FromSeconds(seconds);
            return time.ToString(@"hh\:mm\:ss\.fff");
        }

        private class WhisperResult
        {
            public string Text { get; set; } = string.Empty;
            public List<WhisperSegment> Segments { get; set; } = new List<WhisperSegment>();
        }

        private class WhisperSegment
        {
            public int Id { get; set; }
            public double Start { get; set; }
            public double End { get; set; }
            public string Text { get; set; } = string.Empty;
            public List<WhisperWord> Words { get; set; } = new List<WhisperWord>();
        }

        private class WhisperWord
        {
            public double Start { get; set; }
            public double End { get; set; }
            public string Word { get; set; } = string.Empty;
        }
    }
}
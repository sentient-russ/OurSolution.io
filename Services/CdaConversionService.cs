using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace os.Services
{
    public class CdaConversionService : IHostedService
    {
        private readonly ILogger<CdaConversionService> _logger;
        private readonly ConcurrentDictionary<string, ConversionJob> _conversionJobs = new();
        
        public CdaConversionService(ILogger<CdaConversionService> logger)
        {
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("CDA Conversion Service started");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("CDA Conversion Service stopped");
            return Task.CompletedTask;
        }

        public string QueueConversion(List<IFormFile> cdaFiles, string speakerAbbreviation)
        {
            // Generate a unique ID for this conversion job
            string jobId = Guid.NewGuid().ToString();
            
            var job = new ConversionJob
            {
                Id = jobId,
                Status = "Queued",
                Progress = 0,
                SpeakerAbbreviation = speakerAbbreviation,
                TotalFiles = cdaFiles.Count,
                CompletedFiles = 0,
                StartTime = DateTime.UtcNow
            };
            
            _conversionJobs[jobId] = job;
            
            // Start processing the files asynchronously
            Task.Run(() => ProcessCdaFilesAsync(cdaFiles, jobId));
            
            return jobId;
        }
        
        public ConversionJob GetJobStatus(string jobId)
        {
            if (_conversionJobs.TryGetValue(jobId, out var job))
            {
                return job;
            }
            
            return null;
        }
        
        private async Task ProcessCdaFilesAsync(List<IFormFile> cdaFiles, string jobId)
        {
            if (!_conversionJobs.TryGetValue(jobId, out var job))
            {
                _logger.LogError("Job {JobId} not found", jobId);
                return;
            }
            
            job.Status = "Processing";
            
            try
            {
                // Create temporary directory for processing
                var tempDir = Path.Combine(Path.GetTempPath(), jobId);
                Directory.CreateDirectory(tempDir);
                
                // Save CDA files to temp directory
                for (int i = 0; i < cdaFiles.Count; i++)
                {
                    var file = cdaFiles[i];
                    var filePath = Path.Combine(tempDir, $"track_{i:D2}{Path.GetExtension(file.FileName)}");
                    
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }
                    
                    job.CompletedFiles++;
                    job.Progress = (int)((float)job.CompletedFiles / job.TotalFiles * 50); // First 50% is for saving files
                }
                
                // Create output MP3 file name
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var outputFileName = $"{job.SpeakerAbbreviation}_{timestamp}.mp3";
                var outputPath = Path.Combine("wwwroot", "uploads", outputFileName);
                
                // Use FFmpeg to convert and combine the CDA files
                // Note: This assumes FFmpeg is installed on the server
                var inputFiles = Directory.GetFiles(tempDir)
                    .OrderBy(f => f)
                    .Select(f => $"-i \"{f}\"")
                    .ToList();
                
                var inputFilesArg = string.Join(" ", inputFiles);
                var filterComplex = $"-filter_complex \"concat=n={cdaFiles.Count}:v=0:a=1[out]\" -map \"[out]\"";
                
                var processInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"{inputFilesArg} {filterComplex} -c:a libmp3lame -q:a 2 \"{outputPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                using (var process = new Process { StartInfo = processInfo })
                {
                    process.OutputDataReceived += (sender, e) => 
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            _logger.LogInformation("FFmpeg: {Data}", e.Data);
                        }
                    };
                    
                    process.ErrorDataReceived += (sender, e) => 
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            // FFmpeg outputs progress to stderr
                            if (e.Data.Contains("time="))
                            {
                                // Parse time and update progress
                                job.Progress = 50 + (int)((float)job.CompletedFiles / job.TotalFiles * 50); // Second 50% is for conversion
                            }
                            _logger.LogInformation("FFmpeg: {Data}", e.Data);
                        }
                    };
                    
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    await process.WaitForExitAsync();
                    
                    if (process.ExitCode != 0)
                    {
                        throw new Exception($"FFmpeg exited with code {process.ExitCode}");
                    }
                }
                
                job.Status = "Completed";
                job.Progress = 100;
                job.OutputFileName = outputFileName;
                job.CompletionTime = DateTime.UtcNow;
                
                // Clean up temporary files
                Directory.Delete(tempDir, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting CDA files for job {JobId}", jobId);
                job.Status = "Failed";
                job.ErrorMessage = ex.Message;
            }
        }
    }

    public class ConversionJob
    {
        public string Id { get; set; }
        public string Status { get; set; } // Queued, Processing, Completed, Failed
        public int Progress { get; set; } // 0-100
        public string SpeakerAbbreviation { get; set; }
        public int TotalFiles { get; set; }
        public int CompletedFiles { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? CompletionTime { get; set; }
        public string OutputFileName { get; set; }
        public string ErrorMessage { get; set; }
        public bool Complete { get; set; }
        public bool Error { get; set; }
        public string SuccessMessage { get; set; }
    }
}
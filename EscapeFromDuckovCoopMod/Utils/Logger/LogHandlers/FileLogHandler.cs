using EscapeFromDuckovCoopMod.Utils.Logger.Core;
using EscapeFromDuckovCoopMod.Utils.Logger.Logs;
using ILogHandler = EscapeFromDuckovCoopMod.Utils.Logger.Core.ILogHandler;

namespace EscapeFromDuckovCoopMod.Utils.Logger.LogHandlers
{
    /// <summary>
    /// Standalone file log handler that writes all logs to a text file
    /// </summary>
    public class FileLogHandler : ILogHandler, ILogHandler<Log>, ILogHandler<LabelLog>, ILogHandler<LogHandlerAsyncDecorator.AsyncLog>, IDisposable
    {
        private StreamWriter _logWriter;
        private readonly string _logFilePath;
        private readonly object _writerLock = new object();
        private bool _disposed = false;

        public FileLogHandler(string logDirectory = null)
        {
            // Default log directory uses DUCKOV_GAME_DIRECTORY environment variable
            if (string.IsNullOrEmpty(logDirectory))
            {
                string duckovPath = Environment.GetEnvironmentVariable("DUCKOV_GAME_DIRECTORY");
                if (!string.IsNullOrEmpty(duckovPath))
                {
                    // Create Cooplogs folder in game directory
                    logDirectory = Path.Combine(duckovPath, "Cooplogs");
                }
                else
                {
                    // Fallback to LocalAppData if environment variable not set
                    logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EscapeFromDuckovCoopMod", "Logs");
                }
            }

            // Ensure directory exists
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            // Create log file with timestamp
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            _logFilePath = Path.Combine(logDirectory, $"CoopMod_{timestamp}.log");

            // Initialize the StreamWriter
            try
            {
                _logWriter = new StreamWriter(_logFilePath, append: true, encoding: System.Text.Encoding.UTF8)
                {
                    AutoFlush = true
                };

                // Write header
                _logWriter.WriteLine("=================================================");
                _logWriter.WriteLine($"Escape From Duckov Coop Mod - Log File");
                _logWriter.WriteLine($"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                _logWriter.WriteLine("=================================================");
                _logWriter.WriteLine();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Failed to create log file: {ex.Message}");
            }
        }

        public void Log<TLog>(TLog log) where TLog : struct, ILog
        {
            Log(log.Level, log.ParseToString());
        }

        public void Log(Log log)
        {
            Log(log.Level, log.ParseToString());
        }

        public void Log(LabelLog log)
        {
            Log(log.Level, log.ParseToString());
        }

        public void Log(LogHandlerAsyncDecorator.AsyncLog log)
        {
            // Write timestamp
            WriteLog($"[{log.Timestamp:HH:mm:ss}] ");

            log.LogAction(this);
        }

        public void Log(LogLevel logLevel, string message)
        {
            if (logLevel is LogLevel.None or LogLevel.Custom)
            {
                WriteLineLog(message);
            }
            else
            {
                WriteLineLog($"[{logLevel}] {message}");
            }
        }

        private void WriteLog(string message)
        {
            if (_disposed || _logWriter == null) return;

            lock (_writerLock)
            {
                try
                {
                    _logWriter.Write(message);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"Failed to write log: {ex.Message}");
                }
            }
        }

        private void WriteLineLog(string message)
        {
            if (_disposed || _logWriter == null) return;

            lock (_writerLock)
            {
                try
                {
                    _logWriter.WriteLine(message);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"Failed to write log: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            lock (_writerLock)
            {
                if (_logWriter != null)
                {
                    try
                    {
                        _logWriter.WriteLine();
                        _logWriter.WriteLine("=================================================");
                        _logWriter.WriteLine($"Log ended: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                        _logWriter.WriteLine("=================================================");
                        _logWriter.Flush();
                        _logWriter.Dispose();
                    }
                    catch
                    {
                        // Ignore errors during disposal
                    }
                    _logWriter = null;
                }
            }
        }

        ~FileLogHandler()
        {
            Dispose();
        }
    }
}

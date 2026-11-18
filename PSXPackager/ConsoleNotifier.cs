using System;
using Popstation;
using PSXPackager.Common;
using PSXPackager.Common.Notification;

namespace PSXPackager
{
    public class ConsoleNotifier : NotifierBase
    {
        private int _cursorYPos;
        private long _total;
        private long _lastTicks;
        private int _charsToDelete;
        private readonly int _logLevel;
        // The GUI starts the CLI with redirected output and no console handle.
        // In that mode cursor APIs throw System.IO.IOException on Windows.
        private bool _canUseCursor;
        private int _lastReportedPercent = -1;

        public ConsoleNotifier(int logLevel)
        {
            _logLevel = logLevel;
            _canUseCursor = !Console.IsOutputRedirected;
        }

        private void HideCursor()
        {
            if (!_canUseCursor)
            {
                Console.WriteLine();
                return;
            }

            try
            {
                _cursorYPos = Console.CursorTop;
                Console.CursorVisible = false;
            }
            catch (System.IO.IOException)
            {
                _canUseCursor = false;
                Console.WriteLine();
            }
            catch (System.InvalidOperationException)
            {
                _canUseCursor = false;
                Console.WriteLine();
            }
        }

        private void ShowCursor()
        {
            if (!_canUseCursor) return;
            try { Console.CursorVisible = true; }
            catch (System.IO.IOException) { _canUseCursor = false; }
            catch (System.InvalidOperationException) { _canUseCursor = false; }
        }

        private void ShowProgress(object value, bool alreadyPercent = false)
        {
            var percent = alreadyPercent
                ? Convert.ToInt32(value)
                : _total > 0 ? (int)Math.Round(Convert.ToDouble(value) / _total * 100, 0) : 0;
            percent = Math.Max(0, Math.Min(percent, 100));

            // Print one line per new percentage in redirected mode so GUI/logs
            // can parse it without ANSI control codes or backspace characters.
            if (!_canUseCursor)
            {
                if (percent != _lastReportedPercent)
                {
                    Console.WriteLine($"Progress: {percent}%");
                    _lastReportedPercent = percent;
                }
                return;
            }

            if (DateTime.Now.Ticks - _lastTicks > 100000)
            {
                Overwrite($"{percent}%");
                _lastTicks = DateTime.Now.Ticks;
            }
        }

        private void FinishProgress()
        {
            if (_canUseCursor)
            {
                Overwrite("100%");
                ShowCursor();
                Console.WriteLine();
            }
            else if (_lastReportedPercent != 100)
            {
                Console.WriteLine("Progress: 100%");
            }
            _charsToDelete = 0;
            _lastReportedPercent = -1;
        }

        private int GetLogLevel(PopstationEventEnum @event)
        {
            switch (@event)
            {
                case PopstationEventEnum.ProcessingStart:
                case PopstationEventEnum.ProcessingComplete:
                    return -1;

                case PopstationEventEnum.Error:
                    return -1;

                case PopstationEventEnum.Warning:
                    return 1;

                case PopstationEventEnum.Info:
                    return 3;


                case PopstationEventEnum.FileName:
                    return 0;

                case PopstationEventEnum.GetIsoSize:
                case PopstationEventEnum.ConvertSize:
                case PopstationEventEnum.ExtractSize:
                case PopstationEventEnum.WriteSize:
                    return -1;

                case PopstationEventEnum.ConvertStart:
                case PopstationEventEnum.DiscStart:
                case PopstationEventEnum.ExtractStart:
                case PopstationEventEnum.DecompressStart:

                case PopstationEventEnum.ConvertProgress:
                case PopstationEventEnum.ExtractProgress:
                case PopstationEventEnum.WriteProgress:
                case PopstationEventEnum.DecompressProgress:

                case PopstationEventEnum.ConvertComplete:
                case PopstationEventEnum.ExtractComplete:
                case PopstationEventEnum.DiscComplete:
                case PopstationEventEnum.DecompressComplete:
                    return 2;
            }
            return -1;
        }

        public override void Notify(PopstationEventEnum @event, object value)
        {
            if (GetLogLevel(@event) > _logLevel) return;

            switch (@event)
            {
                case PopstationEventEnum.ProcessingStart:
                    StartDateTime = DateTime.Now;
                    WriteLine(@event, $"Processing started: {StartDateTime.Hour:00}:{StartDateTime.Minute:00}:{StartDateTime.Second:00}");
                    break;

                case PopstationEventEnum.ProcessingComplete:
                    var elapsedSpan = DateTime.Now - StartDateTime;
                    WriteLine(@event, $"Processing completed: {elapsedSpan.TotalHours:00}h {elapsedSpan.Minutes:00}m {elapsedSpan.Seconds:00}s");
                    break;

                case PopstationEventEnum.Error:
                    _charsToDelete = 0;
                    ShowCursor();
                    WriteLine(@event, $"\r\n{value}");
                    break;

                case PopstationEventEnum.FileName:
                case PopstationEventEnum.Info:
                    WriteLine(@event, $"{value}");
                    break;

                case PopstationEventEnum.Warning:
                    var lastColor = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    WriteLine(@event, $"WARNING: {value}");
                    Console.ForegroundColor = lastColor;
                    break;

                case PopstationEventEnum.GetIsoSize:
                    _total = Convert.ToInt64(value);
                    break;

                case PopstationEventEnum.ConvertSize:
                case PopstationEventEnum.ExtractSize:
                case PopstationEventEnum.WriteSize:
                    _total = Convert.ToInt64(value);
                    break;

                case PopstationEventEnum.ConvertStart:
                    Write(@event, $"Converting Disc {value} - ");
                    _lastReportedPercent = -1;
                    HideCursor();
                    break;

                case PopstationEventEnum.DiscStart:
                    Write(@event, $"Writing Disc {value} - ");
                    _lastReportedPercent = -1;
                    HideCursor();
                    break;

                case PopstationEventEnum.ExtractStart:
                    Write(@event, $"Extracting Disc {value} - ");
                    _lastReportedPercent = -1;
                    HideCursor();
                    break;

                case PopstationEventEnum.DecompressStart:
                    Write(@event, $"Decompressing file {value} - ");
                    _lastReportedPercent = -1;
                    HideCursor();
                    break;

                case PopstationEventEnum.ConvertComplete:
                    break;

                case PopstationEventEnum.ExtractComplete:
                case PopstationEventEnum.DiscComplete:
                case PopstationEventEnum.DecompressComplete:
                    FinishProgress();
                    break;

                case PopstationEventEnum.ConvertProgress:
                case PopstationEventEnum.ExtractProgress:
                case PopstationEventEnum.WriteProgress:
                    ShowProgress(value);
                    break;
                case PopstationEventEnum.DecompressProgress:
                    ShowProgress(value, alreadyPercent: true);
                    break;
            }
        }

        private string TimeStamp(PopstationEventEnum @event)
        {
            if (_logLevel < 4) return string.Empty;
            if (IsProgressEvent(@event)) return string.Empty;
            return FormatTimestamp(DateTime.Now);
        }

        private void WriteLine(PopstationEventEnum @event, string text)
        {
            Console.WriteLine(TimeStamp(@event) + text);
        }

        private void Write(PopstationEventEnum @event, string text)
        {
            Console.Write(TimeStamp(@event) + text);
        }

        private void Overwrite(string text)
        {
            // Never issue cursor-control output into a pipe.
            if (!_canUseCursor) return;
            Console.Write(new string('\b', _charsToDelete));
            Console.Write(text);
            _charsToDelete = text.Length;
        }

    }
}
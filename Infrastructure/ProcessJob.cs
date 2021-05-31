using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using MigrasiLogee.Native;

namespace MigrasiLogee.Infrastructure
{
    public record RunProcessResult(string StandardOutput, string StandardError, int ExitCode);

    public class ProcessJob : IDisposable
    {
        private Process _process;
        private SafeJobObjectHandle _handle;
        private ManualResetEventSlim _resetEvent;

        public string ExecutableName { get; set; }
        public string Arguments { get; set; }

        public RunProcessResult StartWaitWithRedirect()
        {
            _resetEvent = new ManualResetEventSlim(false);
            _process = new Process();

            var outputBuffer = new StringBuilder();
            var errorBuffer = new StringBuilder();

            _process.StartInfo = new ProcessStartInfo
            {
                FileName = ExecutableName,
                Arguments = Arguments,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            _process.EnableRaisingEvents = true;
            _process.OutputDataReceived += (_, args) => outputBuffer.AppendLine(args.Data);
            _process.ErrorDataReceived += (_, args) => errorBuffer.AppendLine(args.Data);
            _process.Exited += (_, _) => _resetEvent.Set();

            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
            AttachJobObject();

            _resetEvent.Wait();
            _process.WaitForExit(10 * 1000);

            return new RunProcessResult(outputBuffer.ToString(), errorBuffer.ToString(), _process.ExitCode);
        }

        public void StartJob()
        {
            StopJob();
            _process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ExecutableName,
                    Arguments = Arguments,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                }
            };

            _process.Start();
            AttachJobObject();
        }

        public bool EnsureStarted()
        {
            return _process != null && !_process.HasExited;
        }

        public void StopJob()
        {
            if (_process == null || _process.HasExited)
            {
                return;
            }

            _process.Kill(true);
            _process.WaitForExit(1 * 10 ^ 3);
        }

        public void Dispose()
        {
            if (_process != null)
            {
                StopJob();
                _process.Close();
                _process.Dispose();
            }
            
            if (_handle != null && !_handle.IsClosed)
            {
                _handle.Close();
                _handle.Dispose();
            }

            _resetEvent?.Dispose();
        }

        private void AttachJobObject()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            _handle = NativeMethods.CreateJobObject(IntPtr.Zero, null);
            var extendedInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
            {
                BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
                {
                    LimitFlags = (uint) JOBOBJECT_BASIC_LIMIT_FLAGS.JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
                }
            };

            var length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
            var extendedInfoPtr = Marshal.AllocHGlobal(length);
            Marshal.StructureToPtr(extendedInfo, extendedInfoPtr, false);

            if (!NativeMethods.SetInformationJobObject(_handle, JobObjectInfoType.ExtendedLimitInformation,
                extendedInfoPtr, (uint) length))
                throw new Win32Exception();

            NativeMethods.AssignProcessToJobObject(_handle, _process.Handle);
        }
    }
}

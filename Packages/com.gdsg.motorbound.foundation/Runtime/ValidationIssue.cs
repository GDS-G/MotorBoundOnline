using System;

namespace MotorBound.Foundation
{
    public enum ValidationSeverity
    {
        Warning = 0,
        Error = 1
    }

    [Serializable]
    public struct ValidationIssue
    {
        public ValidationIssue(string path, string message, ValidationSeverity severity = ValidationSeverity.Error)
        {
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
            Severity = severity;
        }

        public string Path { get; }

        public string Message { get; }

        public ValidationSeverity Severity { get; }

        public override string ToString()
        {
            return Severity + " " + Path + ": " + Message;
        }
    }
}

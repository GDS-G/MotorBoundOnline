using System;
using System.IO;
using System.Text;
using MotorBound.Vehicle.Core;
using UnityEngine;

namespace MotorBound.Product
{
    /// <summary>Versioned local prototype save. Catalog compilation, never saved physics values, supplies vehicle behavior.</summary>
    public static class PrototypeGarageSaveStore
    {
        private const long MaximumSaveBytes = 1024 * 1024;

        // Separate zero-default version fields distinguish omission from constructors'
        // normal authoring defaults in Unity's JSON serializer.
        [Serializable] private sealed class SaveVersions
        {
            public int SchemaVersion = 0;
            public ManifestVersions Manifest = null;
        }
        [Serializable] private sealed class ManifestVersions
        {
            public int Revision = 0;
            public PartVersions[] Parts = null;
        }
        [Serializable] private sealed class PartVersions
        {
            public int PartDefinitionRevision = 0;
        }

        public static bool TryLoad(string path, out PrototypeGarageState state, out string message)
        {
            state = null;
            try
            {
                if (!File.Exists(path))
                {
                    message = "No saved garage yet.";
                    return false;
                }

                if (new FileInfo(path).Length > MaximumSaveBytes)
                {
                    message = "Save is too large. The existing file has been preserved.";
                    return false;
                }

                var json = File.ReadAllText(path, Encoding.UTF8);
                var versions = JsonUtility.FromJson<SaveVersions>(json);
                if (versions == null || versions.SchemaVersion != 1 || versions.Manifest == null
                    || versions.Manifest.Revision <= 0 || versions.Manifest.Parts == null)
                {
                    message = "Save schema or required revision is missing or unsupported. The existing file has been preserved.";
                    return false;
                }
                foreach (var part in versions.Manifest.Parts)
                {
                    if (part == null || part.PartDefinitionRevision != 1)
                    {
                        message = "Saved part revision is missing or unsupported. The existing file has been preserved.";
                        return false;
                    }
                }
                var candidate = JsonUtility.FromJson<PrototypeGarageState>(json);
                var issues = PrototypeGarageCatalog.Validate(candidate);
                if (issues.Count != 0)
                {
                    message = "Save could not be loaded: " + issues[0].Message + " The existing file has been preserved.";
                    return false;
                }

                // Also prove the restored assembly compiles before exposing it to the session.
                PrototypeGarageCatalog.Compile(candidate);
                state = candidate;
                message = "Loaded assembly revision " + state.Manifest.Revision + ".";
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            {
                message = "Load failed: " + exception.Message + " The existing file has been preserved.";
                return false;
            }
        }

        public static bool TrySave(string path, PrototypeGarageState state, out string message)
        {
            try
            {
                var issues = PrototypeGarageCatalog.Validate(state);
                if (issues.Count != 0)
                {
                    message = "Cannot save this assembly: " + issues[0].Message;
                    return false;
                }

                // Protect unreadable or newer-format user data from an implicit replacement.
                if (File.Exists(path) && !TryLoad(path, out _, out var existingMessage))
                {
                    message = "Save blocked to protect the existing file. " + existingMessage;
                    return false;
                }

                var directory = Path.GetDirectoryName(Path.GetFullPath(path));
                Directory.CreateDirectory(directory);
                var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.WriteAllText(temporaryPath, JsonUtility.ToJson(state, true), new UTF8Encoding(false));
                    if (!TryLoad(temporaryPath, out _, out var verificationMessage))
                    {
                        message = "Save verification failed: " + verificationMessage;
                        return false;
                    }

                    if (File.Exists(path))
                    {
                        File.Replace(temporaryPath, path, path + ".bak");
                    }
                    else
                    {
                        File.Move(temporaryPath, path);
                    }

                    message = "Saved assembly revision " + state.Manifest.Revision + ".";
                    return true;
                }
                finally
                {
                    if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            {
                message = "Save failed; prior save retained: " + exception.Message;
                return false;
            }
        }
    }
}

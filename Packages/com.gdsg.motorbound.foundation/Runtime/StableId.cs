using System;
using System.Security.Cryptography;
using System.Text;

namespace MotorBound.Foundation
{
    /// <summary>
    /// A persistent, display-name-independent identifier serialized as a canonical GUID string.
    /// </summary>
    [Serializable]
    public struct StableId : IEquatable<StableId>, IComparable<StableId>
    {
        // Public so engine-free catalog data remains serializable by Unity and plain schema tools.
        // Consumers must read Value and construct through the validated APIs.
        public string SerializedValue;

        public StableId(string value)
        {
            if (!Guid.TryParse(value, out var parsed))
            {
                throw new ArgumentException("Stable IDs must be valid GUID strings.", nameof(value));
            }

            SerializedValue = parsed.ToString("D");
        }

        public string Value => Guid.TryParse(SerializedValue, out var parsed)
            ? parsed.ToString("D")
            : Guid.Empty.ToString("D");

        public bool IsEmpty => string.IsNullOrEmpty(SerializedValue) || Value == Guid.Empty.ToString("D");

        public static StableId Empty => new StableId(Guid.Empty.ToString("D"));

        public static StableId New()
        {
            return new StableId(Guid.NewGuid().ToString("D"));
        }

        /// <summary>
        /// Creates a repeatable UUID-shaped ID for first-party catalog definitions.
        /// Changing either input is an identity migration, not a rename.
        /// </summary>
        public static StableId FromCatalogKey(string catalogNamespace, string key)
        {
            if (string.IsNullOrWhiteSpace(catalogNamespace))
            {
                throw new ArgumentException("A catalog namespace is required.", nameof(catalogNamespace));
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("A catalog key is required.", nameof(key));
            }

            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(catalogNamespace.Trim() + ":" + key.Trim());
                var hash = sha256.ComputeHash(bytes);
                var guidBytes = new byte[16];
                Array.Copy(hash, guidBytes, guidBytes.Length);

                // RFC 4122 variant and version-five-shaped deterministic identity.
                guidBytes[7] = (byte)((guidBytes[7] & 0x0f) | 0x50);
                guidBytes[8] = (byte)((guidBytes[8] & 0x3f) | 0x80);
                return new StableId(new Guid(guidBytes).ToString("D"));
            }
        }

        public static bool TryParse(string candidate, out StableId id)
        {
            if (Guid.TryParse(candidate, out var parsed))
            {
                id = new StableId(parsed.ToString("D"));
                return true;
            }

            id = Empty;
            return false;
        }

        public bool Equals(StableId other)
        {
            return StringComparer.Ordinal.Equals(Value, other.Value);
        }

        public int CompareTo(StableId other)
        {
            return StringComparer.Ordinal.Compare(Value, other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is StableId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value;
        }

        public static bool operator ==(StableId left, StableId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(StableId left, StableId right)
        {
            return !left.Equals(right);
        }
    }
}

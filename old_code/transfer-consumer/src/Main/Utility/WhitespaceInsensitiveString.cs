using System.Text.RegularExpressions;

namespace Main.Utility;

public class WhitespaceInsensitiveString : IEquatable<WhitespaceInsensitiveString>
{
   public bool Equals(WhitespaceInsensitiveString? other)
   {
      if (ReferenceEquals(null, other))
      {
         return false;
      }

      if (ReferenceEquals(this, other))
      {
         return true;
      }

      return string.Equals(_unifiedText, other._unifiedText, StringComparison.InvariantCulture);
   }

   public override bool Equals(object? obj)
   {
      if (ReferenceEquals(null, obj))
      {
         return false;
      }

      if (ReferenceEquals(this, obj))
      {
         return true;
      }

      if (obj.GetType() != GetType())
      {
         return false;
      }

      return Equals((WhitespaceInsensitiveString) obj);
   }

   public override int GetHashCode()
   {
      return StringComparer.InvariantCulture.GetHashCode(_unifiedText);
   }

   public static bool operator ==(WhitespaceInsensitiveString? left, WhitespaceInsensitiveString? right)
   {
      return Equals(left, right);
   }

   public static bool operator !=(WhitespaceInsensitiveString? left, WhitespaceInsensitiveString? right)
   {
      return !Equals(left, right);
   }

   private readonly string _input;
   private readonly string _unifiedText;

   public WhitespaceInsensitiveString(string? input)
   {
      if (string.IsNullOrEmpty(input))
      {
         throw new Exception("Input cannot be null or empty");
      }
      _input = input;
      _unifiedText = _input;
      _unifiedText = _unifiedText.Trim();
      _unifiedText = _unifiedText.Replace("\n", "").Replace("\r", "");
      _unifiedText =  Regex.Replace(_unifiedText, @"\s+", " ");
   }

   public string Raw => _input;

   public override string ToString()
   {
      return _unifiedText;
   }
}

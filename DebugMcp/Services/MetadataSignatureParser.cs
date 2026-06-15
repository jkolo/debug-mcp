using ClrDebug;

namespace DebugMcp.Services;

/// <summary>
/// Parses metadata signature blobs (ECMA-335 II.23.2) into CLR type names. Pure, dependency-free
/// logic extracted from <see cref="ProcessDebugger"/> so it can be unit-tested without a debuggee
/// (BUG-015). Token resolution is supplied by the caller as a delegate.
/// </summary>
internal static class MetadataSignatureParser
{
    /// <summary>
    /// Parses a FIELD signature blob and returns the field's type name.
    /// </summary>
    /// <param name="sig">The raw signature blob.</param>
    /// <param name="resolveToken">Resolves a TypeDef/TypeRef metadata token to a type name.</param>
    public static string ParseFieldSignature(byte[] sig, Func<int, string> resolveToken)
    {
        if (sig.Length == 0)
            return "Unknown";

        int pos = 0;
        // FIELD signature starts with the calling convention byte 0x06 (IMAGE_CEE_CS_CALLCONV_FIELD).
        if (sig[pos] == 0x06)
            pos++;

        return ParseType(sig, ref pos, resolveToken);
    }

    /// <summary>Parses a single type from a signature blob at <paramref name="pos"/>.</summary>
    public static string ParseType(byte[] sig, ref int pos, Func<int, string> resolveToken)
    {
        // Skip custom modifiers / pinned / sentinel markers preceding the type.
        while (pos < sig.Length)
        {
            var mod = (CorElementType)sig[pos];
            if (mod is CorElementType.CModReqd or CorElementType.CModOpt)
            {
                pos++;
                DecompressUnsigned(sig, ref pos); // consume the modifier's coded token
            }
            else if (mod is CorElementType.Pinned or CorElementType.Sentinel)
            {
                pos++;
            }
            else
            {
                break;
            }
        }

        if (pos >= sig.Length)
            return "Unknown";

        var et = (CorElementType)sig[pos++];
        switch (et)
        {
            case CorElementType.Void: return "System.Void";
            case CorElementType.Boolean: return "System.Boolean";
            case CorElementType.Char: return "System.Char";
            case CorElementType.I1: return "System.SByte";
            case CorElementType.U1: return "System.Byte";
            case CorElementType.I2: return "System.Int16";
            case CorElementType.U2: return "System.UInt16";
            case CorElementType.I4: return "System.Int32";
            case CorElementType.U4: return "System.UInt32";
            case CorElementType.I8: return "System.Int64";
            case CorElementType.U8: return "System.UInt64";
            case CorElementType.R4: return "System.Single";
            case CorElementType.R8: return "System.Double";
            case CorElementType.String: return "System.String";
            case CorElementType.I: return "System.IntPtr";
            case CorElementType.U: return "System.UIntPtr";
            case CorElementType.Object: return "System.Object";
            case CorElementType.TypedByRef: return "System.TypedReference";
            case CorElementType.Ptr:
                return ParseType(sig, ref pos, resolveToken) + "*";
            case CorElementType.ByRef:
                return "ref " + ParseType(sig, ref pos, resolveToken);
            case CorElementType.SZArray:
                return ParseType(sig, ref pos, resolveToken) + "[]";
            case CorElementType.Var:
                return "T" + DecompressUnsigned(sig, ref pos);
            case CorElementType.MVar:
                return "M" + DecompressUnsigned(sig, ref pos);
            case CorElementType.ValueType:
            case CorElementType.Class:
                return resolveToken(DecodeTypeDefOrRefToken(sig, ref pos));
            case CorElementType.GenericInst:
            {
                var genericType = ParseType(sig, ref pos, resolveToken); // ValueType|Class + token
                var argCount = DecompressUnsigned(sig, ref pos);
                var args = new List<string>();
                for (int a = 0; a < argCount && pos < sig.Length; a++)
                    args.Add(ParseType(sig, ref pos, resolveToken));
                return $"{genericType}<{string.Join(", ", args)}>";
            }
            case CorElementType.Array:
            {
                var elementType = ParseType(sig, ref pos, resolveToken);
                var rank = (int)DecompressUnsigned(sig, ref pos);
                var numSizes = (int)DecompressUnsigned(sig, ref pos);
                for (int s = 0; s < numSizes; s++) DecompressUnsigned(sig, ref pos);
                var numLoBounds = (int)DecompressUnsigned(sig, ref pos);
                for (int l = 0; l < numLoBounds; l++) DecompressUnsigned(sig, ref pos);
                return elementType + "[" + new string(',', Math.Max(0, rank - 1)) + "]";
            }
            default:
                return "Unknown";
        }
    }

    /// <summary>Decompresses an unsigned integer per ECMA-335 II.23.2.</summary>
    public static uint DecompressUnsigned(byte[] sig, ref int pos)
    {
        if (pos >= sig.Length) return 0;
        byte b = sig[pos++];
        if ((b & 0x80) == 0)
            return b;
        if ((b & 0xC0) == 0x80)
        {
            if (pos >= sig.Length) return 0;
            return (uint)(((b & 0x3F) << 8) | sig[pos++]);
        }
        uint value = (uint)((b & 0x1F) << 24);
        if (pos < sig.Length) value |= (uint)(sig[pos++] << 16);
        if (pos < sig.Length) value |= (uint)(sig[pos++] << 8);
        if (pos < sig.Length) value |= sig[pos++];
        return value;
    }

    /// <summary>Decodes a compressed TypeDefOrRef coded index into a full metadata token.</summary>
    public static int DecodeTypeDefOrRefToken(byte[] sig, ref int pos)
    {
        var coded = DecompressUnsigned(sig, ref pos);
        var tag = coded & 0x3;
        var rid = coded >> 2;
        return tag switch
        {
            0 => (int)(0x02000000 | rid), // TypeDef
            1 => (int)(0x01000000 | rid), // TypeRef
            2 => (int)(0x1B000000 | rid), // TypeSpec
            _ => 0
        };
    }
}

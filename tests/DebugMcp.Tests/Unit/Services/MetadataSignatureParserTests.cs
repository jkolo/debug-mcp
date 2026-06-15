using DebugMcp.Services;
using FluentAssertions;

namespace DebugMcp.Tests.Unit.Services;

/// <summary>
/// Unit tests for the metadata signature blob parser (BUG-015) — pure ECMA-335 decoding,
/// no debuggee required.
/// </summary>
public class MetadataSignatureParserTests
{
    // ELEMENT_TYPE constants (ECMA-335 II.23.1.16)
    private const byte FIELD = 0x06;
    private const byte BOOLEAN = 0x02, CHAR = 0x03, I4 = 0x08, I8 = 0x0a, R8 = 0x0d, STRING = 0x0e;
    private const byte VALUETYPE = 0x11, CLASS = 0x12, OBJECT = 0x1c, SZARRAY = 0x1d, GENERICINST = 0x15;
    private const byte CMOD_OPT = 0x20;

    private static string Parse(params byte[] sig) =>
        MetadataSignatureParser.ParseFieldSignature(sig, t => $"Token:{t:X8}");

    [Theory]
    [InlineData(BOOLEAN, "System.Boolean")]
    [InlineData(CHAR, "System.Char")]
    [InlineData(I4, "System.Int32")]
    [InlineData(I8, "System.Int64")]
    [InlineData(R8, "System.Double")]
    [InlineData(STRING, "System.String")]
    [InlineData(OBJECT, "System.Object")]
    public void ParseFieldSignature_PrimitiveTypes_ReturnsClrName(byte elementType, string expected)
    {
        Parse(FIELD, elementType).Should().Be(expected);
    }

    [Fact]
    public void ParseFieldSignature_SzArrayOfInt_ReturnsArrayTypeName()
    {
        Parse(FIELD, SZARRAY, I4).Should().Be("System.Int32[]");
    }

    [Fact]
    public void ParseFieldSignature_ValueTypeToken_ResolvesViaCallback()
    {
        // VALUETYPE + compressed TypeDef coded index for rid 1 (tag 0 => 0x02000001)
        // coded index = (rid << 2) | tag = (1 << 2) | 0 = 4
        Parse(FIELD, VALUETYPE, 0x04).Should().Be("Token:02000001");
    }

    [Fact]
    public void ParseFieldSignature_ClassTypeRefToken_ResolvesToTypeRef()
    {
        // CLASS + coded index for TypeRef rid 1 (tag 1 => 0x01000001): (1 << 2) | 1 = 5
        Parse(FIELD, CLASS, 0x05).Should().Be("Token:01000001");
    }

    [Fact]
    public void ParseFieldSignature_SkipsCustomModifiers()
    {
        // CMOD_OPT + token, then the real type (Int32). The modifier must be skipped.
        Parse(FIELD, CMOD_OPT, 0x05, I4).Should().Be("System.Int32");
    }

    [Fact]
    public void ParseFieldSignature_GenericInstListOfString_FormatsArgs()
    {
        // GENERICINST CLASS <token=0x08 -> tag0/rid2 -> TypeDef 0x02000002> argCount=1 STRING
        Parse(FIELD, GENERICINST, CLASS, 0x08, 0x01, STRING)
            .Should().Be("Token:02000002<System.String>");
    }

    [Fact]
    public void ParseFieldSignature_EmptyBlob_ReturnsUnknown()
    {
        MetadataSignatureParser.ParseFieldSignature([], t => "x").Should().Be("Unknown");
    }

    [Theory]
    [InlineData(new byte[] { 0x7F }, 0x7Fu)]                       // 1-byte form
    [InlineData(new byte[] { 0x80, 0x80 }, 0x80u)]                 // 2-byte form
    [InlineData(new byte[] { 0xC0, 0x00, 0x40, 0x00 }, 0x4000u)]   // 4-byte form
    public void DecompressUnsigned_DecodesPerEcma335(byte[] bytes, uint expected)
    {
        int pos = 0;
        MetadataSignatureParser.DecompressUnsigned(bytes, ref pos).Should().Be(expected);
    }

    [Theory]
    [InlineData(0x00, 0x02000000)] // rid 0, tag 0 => TypeDef
    [InlineData(0x04, 0x02000001)] // rid 1, tag 0 => TypeDef
    [InlineData(0x05, 0x01000001)] // rid 1, tag 1 => TypeRef
    [InlineData(0x06, 0x1B000001)] // rid 1, tag 2 => TypeSpec
    public void DecodeTypeDefOrRefToken_BuildsFullToken(byte coded, int expectedToken)
    {
        int pos = 0;
        MetadataSignatureParser.DecodeTypeDefOrRefToken([coded], ref pos).Should().Be(expectedToken);
    }
}

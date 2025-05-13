using FliegenPilz.Crypto;
using JetBrains.Annotations;
using Xunit;

namespace FliegenPilz.Tests.Crypto;

[TestSubject(typeof(ShandaCipher))]
public class ShandaCipherTest
{
    [Fact]
    public void RoundTrip()
    {
        var data = "abcdef"u8.ToArray();
        ShandaCipher.Encrypt(data);
        Assert.Equal(data, new byte[]
        {
            29,
            112,
            167,
            160,
            140,
            211
        });
        ShandaCipher.Decrypt(data);
        Assert.Equal("abcdef"u8.ToArray(), data);
    }
}
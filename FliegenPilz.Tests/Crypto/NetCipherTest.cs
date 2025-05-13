using FliegenPilz.Crypto;
using JetBrains.Annotations;
using Xunit;

namespace FliegenPilz.Tests.Crypto;

[TestSubject(typeof(NetCipher))]
public class NetCipherTest
{

    [Fact]
    public void Header()
    {
        var key = new RoundKey([82, 48, 120, 232]);
        var key2 = new RoundKey([82, 48, 120, 89]);

        var v83 = new ShroomVersion(83);
        
        var tests = new (ushort len, RoundKey key, ushort ver, uint)[]
        {
            (44, key, 65470, 401217478),
            (2, new RoundKey([70, 114, 122, 210]), 83, 3526087209),
            (24, key2, v83.Invert().Version, 2798429908),
            (627, key, 65452, 363272148),
        };
        
        foreach (var (len, k, ver, ex) in tests)
        {
            var cipher = new NetCipher(k, ver);
            var a = cipher.EncryptHeader(len);
            Assert.Equal(a, ex);
            Assert.Equal(cipher.DecryptHeader(a), len);
        }
    }
}
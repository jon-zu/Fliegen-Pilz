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
        var v65 = new ShroomVersion(65);
        
        var tests = new (ushort len, RoundKey key, ShroomVersion ver, uint)[]
        {
            (44, key, v65.Invert(), 401217478),
            (627, key, v83.Invert(), 363272148),
            (2, new RoundKey([70, 114, 122, 210]), v83, 3526087209),
            (24, key2, v83.Invert(), 2798429908),
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
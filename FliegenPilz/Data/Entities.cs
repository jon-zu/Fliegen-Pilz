using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FliegenPilz.Data;

[Table("accounts")]
public class AccountEntity
{
    [Key]
    [Column("account_id")]
    public AccountId Id { get; set; }

    [Required]
    [Column("username")]
    [MaxLength(32)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [Column("created_at")]
    public DateTime CreatedAtUtc { get; set; }

    public ICollection<CharacterEntity> Characters { get; set; } = new List<CharacterEntity>();
}

[Table("characters")]
public class CharacterEntity
{
    [Key]
    [Column("character_id")]
    public CharacterId Id { get; set; }

    [Column("account_id")]
    public AccountId AccountId { get; set; }

    public AccountEntity? Account { get; set; }

    [Required]
    [MaxLength(12)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("level")]
    public int Level { get; set; } = 1;

    [Column("map_id")]
    public int MapId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAtUtc { get; set; }
}

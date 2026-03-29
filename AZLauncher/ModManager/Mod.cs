using AZLauncher.Models;

namespace AZLauncher.ModManager;

public sealed class Mod : Resource
{
    public Mod(string id, string nick, string description)
        : this(id, nick, nick, description, description, "General", "通用", true)
    {
    }

    public Mod(
        string id,
        string nick,
        string nickZh,
        string description,
        string descriptionZh,
        string category,
        string categoryZh,
        bool isEnabled = true)
        : base(id, nick, nickZh, description, descriptionZh, category, categoryZh, isEnabled)
    {
    }
}

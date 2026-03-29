namespace AZLuncher.Models;

public abstract class Resource
{
    private readonly string id;
    private string nick;
    private string nickZh;
    private readonly string description;
    private readonly string descriptionZh;
    private string category;
    private string categoryZh;
    private bool isEnabled;

    protected Resource(
        string id,
        string nick,
        string nickZh,
        string description,
        string descriptionZh,
        string category,
        string categoryZh,
        bool isEnabled)
    {
        this.id = id;
        this.nick = nick;
        this.nickZh = nickZh;
        this.description = description;
        this.descriptionZh = descriptionZh;
        this.category = category;
        this.categoryZh = categoryZh;
        this.isEnabled = isEnabled;
    }

    public string GetId() => id;

    public string GetNick() => nick;

    public string GetNickZh() => nickZh;

    public string GetDescription() => description;

    public string GetDescriptionZh() => descriptionZh;

    public string GetCategory() => category;

    public string GetCategoryZh() => categoryZh;

    public bool GetEnabled() => isEnabled;

    public bool SetNick(string newNick)
    {
        if (string.IsNullOrWhiteSpace(newNick))
        {
            return false;
        }

        nick = newNick.Trim();
        return true;
    }

    public bool SetNickZh(string newNickZh)
    {
        if (string.IsNullOrWhiteSpace(newNickZh))
        {
            return false;
        }

        nickZh = newNickZh.Trim();
        return true;
    }

    public bool SetCategory(string newCategory)
    {
        if (string.IsNullOrWhiteSpace(newCategory))
        {
            return false;
        }

        category = newCategory.Trim();
        return true;
    }

    public bool SetCategoryZh(string newCategoryZh)
    {
        if (string.IsNullOrWhiteSpace(newCategoryZh))
        {
            return false;
        }

        categoryZh = newCategoryZh.Trim();
        return true;
    }

    public void SetEnabled(bool enabled)
    {
        isEnabled = enabled;
    }
}

public sealed class ResourcePack : Resource
{
    public ResourcePack(string id, string nick, string description)
        : this(id, nick, nick, description, description, "Resource Pack", "资源包", true)
    {
    }

    public ResourcePack(
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

public sealed class ShaderPack : Resource
{
    public ShaderPack(string id, string nick, string description)
        : this(id, nick, nick, description, description, "Shader Pack", "光影包", true)
    {
    }

    public ShaderPack(
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

using System.Collections.Generic;
using System.Linq;
using AZLauncher.ModManager;

namespace AZLauncher.InstanceManger;

public class Instance
{
    private string _nick;
    private string _nickZh;
    private string _id;
    private string _gameVersionId;
    private string _summary;
    private string _summaryZh;
    private string _channel;
    private string _channelZh;
    private string _lastPlayed;
    private string _lastPlayedZh;
    private bool _isRecommended;
    private readonly List<Mod> Mods;

    public string GetNick() => _nick;
    public string GetNickZh() => _nickZh;
    public string GetId() => _id;
    public string GetGameVersionId() => _gameVersionId;
    public string GetSummary() => _summary;
    public string GetSummaryZh() => _summaryZh;
    public string GetChannel() => _channel;
    public string GetChannelZh() => _channelZh;
    public string GetLastPlayed() => _lastPlayed;
    public string GetLastPlayedZh() => _lastPlayedZh;
    public bool GetRecommended() => _isRecommended;
    public List<Mod> GetMods() => Mods;
    public int GetEnabledModCount() => Mods.Count(mod => mod.GetEnabled());

    public Instance(string nick, string id)
        : this(
            nick,
            nick,
            id,
            id,
            string.Empty,
            string.Empty,
            "Primary",
            "主力",
            "Played recently",
            "最近游玩",
            false)
    {
    }

    public Instance(
        string nick,
        string nickZh,
        string id,
        string gameVersionId,
        string summary,
        string summaryZh,
        string channel,
        string channelZh,
        string lastPlayed,
        string lastPlayedZh,
        bool isRecommended)
    {
        _nick = nick;
        _nickZh = nickZh;
        _id = id;
        _gameVersionId = gameVersionId;
        _summary = summary;
        _summaryZh = summaryZh;
        _channel = channel;
        _channelZh = channelZh;
        _lastPlayed = lastPlayed;
        _lastPlayedZh = lastPlayedZh;
        _isRecommended = isRecommended;
        Mods = [];
    }

    public bool SetNick(string nick)
    {
        if (string.IsNullOrWhiteSpace(nick))
        {
            return false;
        }

        _nick = nick.Trim();
        return true;
    }

    public bool SetNickZh(string nickZh)
    {
        if (string.IsNullOrWhiteSpace(nickZh))
        {
            return false;
        }

        _nickZh = nickZh.Trim();
        return true;
    }

    public bool SetSummary(string summary, string summaryZh)
    {
        if (string.IsNullOrWhiteSpace(summary) || string.IsNullOrWhiteSpace(summaryZh))
        {
            return false;
        }

        _summary = summary.Trim();
        _summaryZh = summaryZh.Trim();
        return true;
    }

    public bool SetChannel(string channel, string channelZh)
    {
        if (string.IsNullOrWhiteSpace(channel) || string.IsNullOrWhiteSpace(channelZh))
        {
            return false;
        }

        _channel = channel.Trim();
        _channelZh = channelZh.Trim();
        return true;
    }

    public bool SetLastPlayed(string lastPlayed, string lastPlayedZh)
    {
        if (string.IsNullOrWhiteSpace(lastPlayed) || string.IsNullOrWhiteSpace(lastPlayedZh))
        {
            return false;
        }

        _lastPlayed = lastPlayed.Trim();
        _lastPlayedZh = lastPlayedZh.Trim();
        return true;
    }

    public void SetRecommended(bool isRecommended)
    {
        _isRecommended = isRecommended;
    }

    public bool AddMod(Mod mod)
    {
        if (mod is null)
        {
            return false;
        }

        if (Mods.Any(existing => existing.GetId() == mod.GetId()))
        {
            return false;
        }

        Mods.Add(mod);
        return true;
    }

    public bool RemoveMod(string modId)
    {
        var mod = Mods.FirstOrDefault(existing => existing.GetId() == modId);
        if (mod is null)
        {
            return false;
        }

        return Mods.Remove(mod);
    }
}

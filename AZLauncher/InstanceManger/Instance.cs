using System.Collections.Generic;
using System.Linq;
using AZLauncher.ModManager;
using AZLauncher.Models;

namespace AZLauncher.InstanceManger;

public class Instance
{
    private string _nick;
    private string _nickZh;
    private string _id;
    private string _gameVersionId;
    private string _launchVersionId;
    private string _summary;
    private string _summaryZh;
    private string _channel;
    private string _channelZh;
    private string _lastPlayed;
    private string _lastPlayedZh;
    private LoaderKind? _loaderKind;
    private string _loaderVersion;
    private bool _isDetected;
    private string _detectedRoot;
    private bool _isRecommended;
    private readonly List<Mod> Mods;

    public string GetNick() => _nick;
    public string GetNickZh() => _nickZh;
    public string GetId() => _id;
    public string GetGameVersionId() => _gameVersionId;
    public string GetLaunchVersionId() => string.IsNullOrWhiteSpace(_launchVersionId) ? _gameVersionId : _launchVersionId;
    public string GetSummary() => _summary;
    public string GetSummaryZh() => _summaryZh;
    public string GetChannel() => _channel;
    public string GetChannelZh() => _channelZh;
    public string GetLastPlayed() => _lastPlayed;
    public string GetLastPlayedZh() => _lastPlayedZh;
    public LoaderKind? GetLoaderKind() => _loaderKind;
    public string GetLoaderVersion() => _loaderVersion;
    public bool GetIsDetected() => _isDetected;
    public string GetDetectedRoot() => _detectedRoot;
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
            false,
            id,
            null,
            string.Empty,
            false,
            string.Empty)
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
        bool isRecommended,
        string? launchVersionId = null,
        LoaderKind? loaderKind = null,
        string? loaderVersion = null,
        bool isDetected = false,
        string? detectedRoot = null)
    {
        _nick = nick;
        _nickZh = nickZh;
        _id = id;
        _gameVersionId = gameVersionId;
        _launchVersionId = string.IsNullOrWhiteSpace(launchVersionId) ? gameVersionId : launchVersionId.Trim();
        _summary = summary;
        _summaryZh = summaryZh;
        _channel = channel;
        _channelZh = channelZh;
        _lastPlayed = lastPlayed;
        _lastPlayedZh = lastPlayedZh;
        _loaderKind = loaderKind;
        _loaderVersion = loaderVersion?.Trim() ?? string.Empty;
        _isDetected = isDetected;
        _detectedRoot = detectedRoot?.Trim() ?? string.Empty;
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

    public bool SetGameVersionId(string gameVersionId)
    {
        if (string.IsNullOrWhiteSpace(gameVersionId))
        {
            return false;
        }

        _gameVersionId = gameVersionId.Trim();
        if (string.IsNullOrWhiteSpace(_launchVersionId))
        {
            _launchVersionId = _gameVersionId;
        }

        return true;
    }

    public bool SetLaunchVersionId(string launchVersionId)
    {
        if (string.IsNullOrWhiteSpace(launchVersionId))
        {
            return false;
        }

        _launchVersionId = launchVersionId.Trim();
        return true;
    }

    public void SetLoader(LoaderKind? loaderKind, string? loaderVersion)
    {
        _loaderKind = loaderKind;
        _loaderVersion = loaderVersion?.Trim() ?? string.Empty;
    }

    public void SetDetectionState(bool isDetected, string? detectedRoot)
    {
        _isDetected = isDetected;
        _detectedRoot = detectedRoot?.Trim() ?? string.Empty;
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

using System.Text;
using System.Text.RegularExpressions;

namespace RekordboxSetupCloner;

// Keep this explicit allowlist aligned with Sources/RekordboxSetupCloner/SettingsPolicy.swift.
public static class SettingsPolicy
{
    public const string MainFile = "rekordbox3.settings", PlayFile = "PlaySettings.xml",
        SamplerFile = "SamplerSettings1.xml", BrowserFile = "browseSetting.xml",
        GrooveFile = "GrooveCircuitSettings.xml", VideoFile = "VideoSettings.xml";
    public static readonly string[] PreferenceFiles = [MainFile, PlayFile, SamplerFile, BrowserFile, GrooveFile, VideoFile, "EditSettings.xml"];
    public const int MaxFileSize = 8 * 1024 * 1024, MaxArchiveSize = 64 * 1024 * 1024, MaxFiles = 512;
    public static readonly Dictionary<string, SettingsGroup> PropertyGroups = CreatePropertyGroups();

    private static Dictionary<string, SettingsGroup> CreatePropertyGroups()
    {
        var result = new Dictionary<string, SettingsGroup>(StringComparer.Ordinal);
        void Add(SettingsGroup group, string names)
        {
            foreach (var name in names.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)) result[name] = group;
        }
        Add(SettingsGroup.stems, """
            TrackSeparationEnable Enable4Stems PartLayout CustomPartLayout ActivePartMode
            StemWaveformDisplay PartInstDoublesDirection ResetMuteSoloStatusOnLoaded TakeoverEqPartIso
            PartAnalysisQuality IncreaseMemoryPartAnalysis ApplyMultithreadingPartAnalysis
            """);
        Add(SettingsGroup.waveforms, """
            ColorType ColorTypeV600 WholeWaveType WaveTypeFull UseVertexWave VertexWaveLineWidth
            BigBeat ZoomWaveHorizontalLayoutType ZoomWaveVerticalLayoutType BeatCountDisplayMode
            BeatCountDisplayStyle MixPointLinkShowBeatGrid MixPointLinkShowBeatGridWhenZoomOut
            Player_WaveViewRatioMPL WaveControlOpen WaveControlPage MixPointLinkShowTimeScale
            DrawLoopArea showPhrase showPhraseFull showPhraseNameAlways showVocalZoom showVocalWhole
            DvsWaveViewBpmScaleMode WaveClickPlayEnabled WaveImageWidth2 scrollModeCenter
            """);
        Add(SettingsGroup.layout, """
            ColorThemeMode WindowLayout DjLayout Layout HidePlatter HideDeckInfo PadModeLayoutType
            FxPanelLayoutType PianoPlayPanelDisplay
            MenuFontSize sourceFontSize artworkSize lineSpaceSize showPreviewCue showBpmSyncRate
            ShowOriginalKey KeyStringSetting showTooltips showpalette showpalettedjapp
            ControllTabPanelMode RzxBarType ZoomFieldOpen MatchingFieldOpen
            MenuFontName MenuFontStyle sourceFontStyle language
            showExplore showHCBL showItune showRbXml showAllItunesPlaylist
            showSoundCloud showBeatport showTidal showSpotify showAppleMusic showInflyte showCloudDirectPlay
            showPlaylistAllTracks showPlaylistSearch showPlaylistTrackNum showSearchMobile
            showAdvancedRelatedTracks showTrackSuggestion EnabledInlineEditWithDblClick TrafficLightRange
            """);
        Add(SettingsGroup.performance, """
            multiBpmSyncMode DeckQuantizeMode InputQuantizeBeat HotCueQuantizeBeat LoopQuantizeBeat
            ReverseQuantizeBeat DeckQuantize DeckQuantizeInOneGo
            InputQuantizeStatus HotCueQuantizeStatus LoopQuantizeStatus ReverseQuantizeStatus
            hotCueAutoLoad HotCueColorType HotCueSampler AutoCueLeveldB DeckJogSpeed jogSamplerMode
            JogAntiDriftSetting JogStartTime JogCutterHalveSpeed JogCutterThresholdBpmToHalve
            JogCutterJumpLastCue EqualizerMode EqualizerType ChFaderCurve CrossFaderCurve DeckJogIndicator
            AutoGain SmartCueEnabled BeatSyncType DeckLoadMode DeckLoadLock DeckMemoryCueCallLock
            DeckLoadingStartPosition DeckNeedleLock AutoInstantDoubles ActiveLoopPlayEnabled
            CrossFaderReverse CrossFaderCutLag
            AutoPlay AutoBeatLoop ExportBeatJump MixPointLinkEnable MixPointAutoChanged MixPointTypeDeult
            MixPointLinkBpmSyncAutoChange MixPointLinkZoomOutLimit FaderForSmartFader
            ShouldUseRecommendedSmartFaderCurve AutoMixRepeatMode AutoMixRandomMode kAutoMixRemovePlayedTrack
            AutoMixBeforeTriggerEndTimeMs kAutoMixCrossfadeRate kAutoMixCrossfadeTiming
            metronomeEnabled metronomeVolume selectMetronome
            """);
        Add(SettingsGroup.effects, "SmartCfxPreset SmartCfxPresetPosition EffectorReleaseFxUnitNum EffectorReleaseFxOnCfx EffectorCFxOnSampler");
        Add(SettingsGroup.sampler, """
            SamplerSlotIs16 SamplerPanelAutoOpen SamplerLayout SamplerColorType
            SamplerLoadLockAtLoadedSlot SlicerCaptureSaveBank ShouldLoopCaptureIfEmptySlot
            SequenceLoadAutoPlay VelocityCurve SequencerQuantize SequencerQuantizeStatus
            """);
        Add(SettingsGroup.groove, "GrooveCircuitEnable DrumCaptureStem DrumSwapSlotLoadLock ShowTitleToDrumSwapSlot");
        Add(SettingsGroup.analysis, """
            trackAnalysisMode trackAnalysisProcessMode AutoAnalysis enableKeyAnalysis
            HighPrecisionBeatAnalyze trackHiPrecisionBeatAnalysisMode trackAnalysisSettingsBpm
            trackAnalysisBpmRange trackAnalysisSettingsSongStruct trackAnalysisSettingsVocalDetect
            trackAnalysisSettingsNetworkAnalysis TrackAnalysisSettingsEmbedding
            initIntelligentCue allowAutoMemoryCue AutoDetectCue DetectCue AutoDetectCueType askAnalysisSettingWindow
            """);
        Add(SettingsGroup.recording, """
            RecordTrigger RecStopTrigger RecSilenceLevelThreshold RecSilenceDurationThreshold
            RecAutoTrackSplit_ExportMode RecAutoTrackSplit_PerformanceMode RecLevelNormalize RecTrackInfoInputWizard
            """);
        Add(SettingsGroup.controller, """
            FaderStart BackSpinLength JogRingColor JogIllumination JogLedBrightness SlipFlashMode SlipBlinkMode
            JogDisplayMode JogDispBeatScalerIsOn JogDispArtWorkIsOn JogDispTimeMode JogDispBrightness
            MixerOledBrightness LevelMeter
            """);
        Add(SettingsGroup.visuals, """
            VideoEnable VideoTFXActive VideoTFXMaxNum VideoTFXFavorite VideoQuality VideoTFXAutoSpeed
            VideoTextAnimation VideoImageAnimation VideoDelayTime VideoDeck1Mute VideoDeck2Mute
            VideoBrightnessChannelFaderDeck1 VideoBrightnessChannelFaderDeck2 VideoFxIncleaseToRight
            LightingEnable DisableLighting LightingEnableFader LightingEnableSuspend LightingDelayTime
            LightingEnableThumbnail LightingEnableDeck34 LightingQuantize
            """);
        for (var i = 1; i <= 24; i++) result[$"VideoTouchFxBeatFxAsign_{i}"] = SettingsGroup.visuals;
        for (var i = 1; i <= 6; i++) result[$"VideoTouchFxColorFxAsign_{i}"] = SettingsGroup.visuals;
        Add(SettingsGroup.layout, """
            AIRecommendCriteriaBPMEnabled AIRecommendCriteriaBPM AIRecommendCriteriaKeyEnabled AIRecommendCriteriaKey
            AIRecommendCriteriaGenreSame AIRecommendCriteriaVocalsEnabled AIRecommendCriteriaVocals
            """);
        for (var i = 0; i <= 5; i++) result[$"AIRecommendCriteriaProviderFlag_{i}"] = SettingsGroup.layout;
        Add(SettingsGroup.mappings, "performaceKeyMapping");
        result["PadEditor Assign"] = SettingsGroup.mappings;
        foreach (var panel in new[] { "FX", "SAMPLER", "MIXER", "REC", "VIDEO", "REC_EXPORT_MODE", "LIGHTING", "MIX_POINT_LINK", "GROOVE_CIRCUIT" })
            result[$"ShowPanel_{panel}"] = SettingsGroup.layout;
        for (var deck = 0; deck <= 3; deck++)
        {
            foreach (var suffix in new[] { "WaveViewRatio", "WaveViewRatioDJ", "WaveViewRatioMPL" }) result[$"Player{deck}_{suffix}"] = SettingsGroup.waveforms;
            foreach (var suffix in new[] { "MasterTempo", "TimeMode", "AutoBeatLoopID_PlayerControllPanel" }) result[$"Player{deck}_{suffix}"] = SettingsGroup.performance;
        }
        for (var deck = 1; deck <= 4; deck++)
            foreach (var name in new[] { $"AutoCueEnableDeck{deck}", $"Player{deck}_Quantize", $"Player{deck}_BeatJumpBeat", $"BeatJumpPageDeck{deck}" }) result[name] = SettingsGroup.performance;
        foreach (var color in new[] { "ROLL", "SWEEP", "FLANGER", "V.BRAKE(RFX)", "ECHO", "REVERB", "ECHO(RFX)", "TRANS", "CRUSH", "FILTER LFO", "BACKSPIN(RFX)", "MT DELAY", "DUB ECHO", "SPACE", "SLIP LOOP", "REV ROLL", "DELAY", "SPIRAL", "PHASER", "ROBOT", "SLIP ROLL", "UP ECHO", "DOWN ECHO", "REV DELAY", "PAN", "CHORUS", "NOISE LFO", "PITCH", "HPF", "LPF", "NOISE(CFX)", "GATE COMP", "BPF ECHO", "NOISE(RMX)", "SPIRAL UP", "REVERB UP", "HPF ECHO", "LPF ECHO", "CRUSH ECHO", "SPIRAL DOWN", "REVERB DOWN" })
            result[$"PadFxColor{color}"] = SettingsGroup.effects;
        return result;
    }

    public static readonly HashSet<string> SamplerKeys = ["visibleBankL", "quantize", "sync", "master_bpm"];
    public static readonly HashSet<string> PlayKeys = Enumerable.Range(0, 4).SelectMany(d =>
        new[] { "TempoRange", "VirtualDeckRpm", "Vinyl", "CrossFaderAssign", "BrakeValue", "StartValue" }.Select(k => $"{k}{d}")).ToHashSet();
    public static readonly HashSet<string> BrowserScalarKeys = new[] { "ShowEditBrowser", "SearchButtonToggle_Main", "SearchButtonToggle_Sub", "TrafficLightKeyEnabled", "RelatedTracksKeySortColumnID", "RelatedTracksKeySortForward", "RelatedTracksKeyEnabledSelectedTree" }
        .Concat(new[] { "bpm", "key", "rate", "color", "mytag0", "mytag1", "mytag2", "mytag3" }.SelectMany(f => new[] { $"ColumnFilterToggle_{f}", $"ColumnFilterSelector_{f}" })).ToHashSet();
    public static readonly HashSet<string> BrowserStructureKeys = new[] { "ArtworkStatus-TagList", "ArtworkStatus-TrackList" }
        .Concat(new[] { "TaglistTracks", "RequestCatalogTracks", "CollectionTracks", "CollectionTracks-sub", "CollectionTracks-automix", "PlaylistTracks", "PlaylistTracks-sub", "PlaylistTracks-automix", "SpotifyTracks", "FolderTracks", "RelatedTracks", "SamplerTracks", "DeviceTracks", "Recordings", "DevicePlaylistTracks", "HistoryTracks", "SoundCloudTracks", "BeatportTracks", "TidalTracks", "AppleMusicTracks" }
            .SelectMany(c => new[] { "", "-AttributeColumn", "-FullArtwork", "-FullArtwork-AttributeColumn" }.Select(s => $"TableHeader-{c}{s}"))).ToHashSet();

    public static SettingsGroup? Group(string key, string file, int schemaVersion = 2)
    {
        if (file == MainFile)
        {
            if (schemaVersion == 1)
            {
                if (new[] { "SamplerPanelAutoOpen", "SamplerLayout", "SamplerColorType" }.Contains(key)) return SettingsGroup.layout;
                if (new[] { "SamplerLoadLockAtLoadedSlot", "SlicerCaptureSaveBank", "ShouldLoopCaptureIfEmptySlot", "SequenceLoadAutoPlay", "VelocityCurve", "SequencerQuantize", "SequencerQuantizeStatus" }.Contains(key)) return SettingsGroup.performance;
            }
            return PropertyGroups.TryGetValue(key, out var group) ? group : null;
        }
        if (file == PlayFile && PlayKeys.Contains(key)) return SettingsGroup.performance;
        if (file == SamplerFile && (SamplerKeys.Contains(key) || key.StartsWith("SamplerSet/", StringComparison.Ordinal) && SamplerKeys.Contains(key[11..]))) return SettingsGroup.sampler;
        if (file == BrowserFile && BrowserScalarKeys.Contains(key)) return SettingsGroup.layout;
        if (file == GrooveFile && Enumerable.Range(1, 4).Any(d => new[] { $"visibleBank{d}", $"singleMulti{d}", $"fxType{d}" }.Contains(key))) return SettingsGroup.groove;
        if (file == VideoFile && new[] { "ImageOverlay/transparency", "ImageOverlay/size", "TextOverlay/transparency", "TextOverlay/size" }.Contains(key)) return SettingsGroup.visuals;
        if (file == "EditSettings.xml" && new[] { "LoopMode", "ShowToolPanel", "SelectedPadSelectedTab" }.Contains(key)) return SettingsGroup.layout;
        return null;
    }

    public static SettingsGroup? StructureGroup(string key, string file) =>
        file == BrowserFile && BrowserStructureKeys.Contains(key) ? SettingsGroup.layout :
        file == GrooveFile && Enumerable.Range(1, 4).Any(d => key == $"FxSet{d}") ? SettingsGroup.groove : null;

    public static HashSet<SettingsGroup> Groups(string file) => file switch
    {
        MainFile => PropertyGroups.Values.ToHashSet(),
        PlayFile => [SettingsGroup.performance],
        SamplerFile => [SettingsGroup.sampler],
        BrowserFile or "EditSettings.xml" => [SettingsGroup.layout],
        GrooveFile => [SettingsGroup.groove],
        VideoFile => [SettingsGroup.visuals],
        _ => []
    };

    public static bool ValidRelativePath(string path)
    {
        if (string.IsNullOrEmpty(path) || Encoding.UTF8.GetByteCount(path) > 512 || path.Any(char.IsControl) || path.Contains('\\')) return false;
        return path.Split('/').All(p => p.Length > 0 && !p.StartsWith('.') && !p.EndsWith('.') && !p.EndsWith(' ') &&
            p.IndexOfAny([':', '*', '?', '"', '<', '>', '|']) < 0 &&
            !Regex.IsMatch(p.Split('.')[0], @"^(CON|PRN|AUX|NUL|COM[0-9¹²³]|LPT[0-9¹²³])$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
    }

    public static SettingsGroup? FileGroup(string path)
    {
        if (!ValidRelativePath(path)) return null;
        if (new[] { "PadFxSettings.xml", "FxUnitSettings.xml", "FxUnitSettings6.xml", "FxUnitSettings7.xml", "MFXSettings.xml" }.Contains(path)) return SettingsGroup.effects;
        var parts = path.Split('/');
        if (parts.Length == 2 && parts[0] == "KeyMappings" && parts[1].EndsWith(".mappings", StringComparison.Ordinal)) return SettingsGroup.mappings;
        if (parts.Length == 2 && parts[0] == "MidiMappings" && parts[1].EndsWith(".midi.csv", StringComparison.Ordinal)) return SettingsGroup.mappings;
        if (parts[0] == "pad")
        {
            if (parts.Length == 2 && (parts[1] == "Version" || parts[1].EndsWith(".pad.csv", StringComparison.Ordinal))) return SettingsGroup.mappings;
            if (parts.Length == 3 && Regex.IsMatch(parts[1], @"^assign[0-9]+$") && parts[2].EndsWith(".pad.csv", StringComparison.Ordinal)) return SettingsGroup.mappings;
        }
        return null;
    }
}

import Foundation

enum SettingsGroup: String, Codable, CaseIterable, Identifiable {
    case stems, waveforms, layout, performance, sampler, groove, effects, mappings
    case analysis, recording, controller, visuals

    var id: String { rawValue }
    static let defaultSelection = Set(allCases).subtracting([.visuals])
    var title: String {
        switch self {
        case .stems: return "STEMS"
        case .waveforms: return "Waveforms"
        case .layout: return "Screen layout"
        case .performance: return "Deck & mixer preferences"
        case .effects: return "Pad FX & effects"
        case .mappings: return "Keyboard, MIDI & pad mappings"
        case .sampler: return "Sampler & sequencer"
        case .groove: return "Groove Circuit"
        case .analysis: return "Analysis preferences"
        case .recording: return "Recording preferences"
        case .controller: return "Controller feel & display"
        case .visuals: return "Video & lighting"
        }
    }
    var detail: String {
        switch self {
        case .stems: return "Activation, speed/quality, memory, multithreading and part layout"
        case .waveforms: return "Colours, zoom, beat grid and vocal display"
        case .layout: return "Decks, panels, browser columns, fonts and source visibility"
        case .performance: return "Deck behaviour, quantize, Auto Mix, Mix Point Link and faders"
        case .effects: return "Pad FX banks, effect units and Merge FX"
        case .mappings: return "Custom shortcuts, controller mappings and pad editor layouts"
        case .sampler: return "Layout, banks, tempo, sync, quantize and capture behaviour; no samples"
        case .groove: return "Activation, drum capture behaviour and effect preferences"
        case .analysis: return "BPM, key, phrase, vocal and cue-analysis options; no analysis data"
        case .recording: return "Triggers, silence detection, splitting and normalization; no paths"
        case .controller: return "Jog displays, LEDs, slip indicators, backspin and fader start"
        case .visuals: return "Video effects, overlays and lighting behaviour; no media or fixtures"
        }
    }
    var symbol: String {
        switch self {
        case .stems: return "waveform.path"
        case .waveforms: return "waveform"
        case .layout: return "rectangle.split.2x2"
        case .performance: return "slider.horizontal.3"
        case .effects: return "square.grid.3x3"
        case .mappings: return "pianokeys"
        case .sampler: return "square.grid.4x3.fill"
        case .groove: return "waveform.path.ecg"
        case .analysis: return "magnifyingglass"
        case .recording: return "record.circle"
        case .controller: return "dial.low"
        case .visuals: return "lightbulb"
        }
    }
}

/// Deliberately explicit: newly introduced rekordbox properties are excluded until reviewed.
enum SettingsPolicy {
    static let mainFile = "rekordbox3.settings"
    static let playFile = "PlaySettings.xml"
    static let samplerFile = "SamplerSettings1.xml"
    static let browserFile = "browseSetting.xml"
    static let grooveFile = "GrooveCircuitSettings.xml"
    static let videoFile = "VideoSettings.xml"
    /// These files must be filtered and merged, never copied wholesale into a portable backup.
    static let preferenceFiles = [mainFile, playFile, samplerFile, browserFile, grooveFile, videoFile, "EditSettings.xml"]
    static let maxFileSize = 8 * 1024 * 1024
    static let maxArchiveSize = 64 * 1024 * 1024
    static let maxFiles = 512

    static let propertyGroups: [String: SettingsGroup] = {
        var result: [String: SettingsGroup] = [:]
        func add(_ group: SettingsGroup, _ names: String) {
            for name in names.split(whereSeparator: \.isWhitespace) { result[String(name)] = group }
        }
        add(.stems, """
        TrackSeparationEnable Enable4Stems PartLayout CustomPartLayout ActivePartMode
        StemWaveformDisplay PartInstDoublesDirection ResetMuteSoloStatusOnLoaded TakeoverEqPartIso
        PartAnalysisQuality IncreaseMemoryPartAnalysis ApplyMultithreadingPartAnalysis
        """)
        add(.waveforms, """
        ColorType ColorTypeV600 WholeWaveType WaveTypeFull UseVertexWave VertexWaveLineWidth
        BigBeat ZoomWaveHorizontalLayoutType ZoomWaveVerticalLayoutType BeatCountDisplayMode
        BeatCountDisplayStyle MixPointLinkShowBeatGrid MixPointLinkShowBeatGridWhenZoomOut
        Player_WaveViewRatioMPL WaveControlOpen WaveControlPage MixPointLinkShowTimeScale
        DrawLoopArea showPhrase showPhraseFull showPhraseNameAlways showVocalZoom showVocalWhole
        DvsWaveViewBpmScaleMode WaveClickPlayEnabled WaveImageWidth2 scrollModeCenter
        """)
        add(.layout, """
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
        """)
        add(.performance, """
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
        """)
        add(.effects, """
        SmartCfxPreset SmartCfxPresetPosition EffectorReleaseFxUnitNum EffectorReleaseFxOnCfx
        EffectorCFxOnSampler
        """)
        add(.sampler, """
        SamplerSlotIs16 SamplerPanelAutoOpen SamplerLayout SamplerColorType
        SamplerLoadLockAtLoadedSlot SlicerCaptureSaveBank ShouldLoopCaptureIfEmptySlot
        SequenceLoadAutoPlay VelocityCurve SequencerQuantize SequencerQuantizeStatus
        """)
        add(.groove, "GrooveCircuitEnable DrumCaptureStem DrumSwapSlotLoadLock ShowTitleToDrumSwapSlot")
        add(.analysis, """
        trackAnalysisMode trackAnalysisProcessMode AutoAnalysis enableKeyAnalysis
        HighPrecisionBeatAnalyze trackHiPrecisionBeatAnalysisMode trackAnalysisSettingsBpm
        trackAnalysisBpmRange trackAnalysisSettingsSongStruct trackAnalysisSettingsVocalDetect
        trackAnalysisSettingsNetworkAnalysis TrackAnalysisSettingsEmbedding
        initIntelligentCue allowAutoMemoryCue AutoDetectCue DetectCue AutoDetectCueType askAnalysisSettingWindow
        """)
        add(.recording, """
        RecordTrigger RecStopTrigger RecSilenceLevelThreshold RecSilenceDurationThreshold
        RecAutoTrackSplit_ExportMode RecAutoTrackSplit_PerformanceMode RecLevelNormalize RecTrackInfoInputWizard
        """)
        add(.controller, """
        FaderStart BackSpinLength JogRingColor JogIllumination JogLedBrightness SlipFlashMode SlipBlinkMode
        JogDisplayMode JogDispBeatScalerIsOn JogDispArtWorkIsOn JogDispTimeMode JogDispBrightness
        MixerOledBrightness LevelMeter
        """)
        add(.visuals, """
        VideoEnable VideoTFXActive VideoTFXMaxNum VideoTFXFavorite VideoQuality VideoTFXAutoSpeed
        VideoTextAnimation VideoImageAnimation VideoDelayTime VideoDeck1Mute VideoDeck2Mute
        VideoBrightnessChannelFaderDeck1 VideoBrightnessChannelFaderDeck2 VideoFxIncleaseToRight
        LightingEnable DisableLighting LightingEnableFader LightingEnableSuspend LightingDelayTime
        LightingEnableThumbnail LightingEnableDeck34 LightingQuantize
        """)
        for number in 1...24 { result["VideoTouchFxBeatFxAsign_\(number)"] = .visuals }
        for number in 1...6 { result["VideoTouchFxColorFxAsign_\(number)"] = .visuals }
        add(.layout, """
        AIRecommendCriteriaBPMEnabled AIRecommendCriteriaBPM AIRecommendCriteriaKeyEnabled AIRecommendCriteriaKey
        AIRecommendCriteriaGenreSame AIRecommendCriteriaVocalsEnabled AIRecommendCriteriaVocals
        """)
        // Genre-list entries can reference personal library categories, so they are deliberately not copied.
        for number in 0...5 { result["AIRecommendCriteriaProviderFlag_\(number)"] = .layout }
        add(.mappings, "performaceKeyMapping") // This spelling is used by rekordbox itself.
        result["PadEditor Assign"] = .mappings
        for panel in ["FX", "SAMPLER", "MIXER", "REC", "VIDEO", "REC_EXPORT_MODE", "LIGHTING", "MIX_POINT_LINK", "GROOVE_CIRCUIT"] {
            result["ShowPanel_\(panel)"] = .layout
        }
        for deck in 0...3 {
            for suffix in ["WaveViewRatio", "WaveViewRatioDJ", "WaveViewRatioMPL"] {
                result["Player\(deck)_\(suffix)"] = .waveforms
            }
            for suffix in ["MasterTempo", "TimeMode", "AutoBeatLoopID_PlayerControllPanel"] {
                result["Player\(deck)_\(suffix)"] = .performance
            }
        }
        for deck in 1...4 {
            for name in ["AutoCueEnableDeck\(deck)", "Player\(deck)_Quantize", "Player\(deck)_BeatJumpBeat", "BeatJumpPageDeck\(deck)"] {
                result[name] = .performance
            }
        }
        let colors = ["ROLL", "SWEEP", "FLANGER", "V.BRAKE(RFX)", "ECHO", "REVERB", "ECHO(RFX)",
                      "TRANS", "CRUSH", "FILTER LFO", "BACKSPIN(RFX)", "MT DELAY", "DUB ECHO", "SPACE",
                      "SLIP LOOP", "REV ROLL", "DELAY", "SPIRAL", "PHASER", "ROBOT", "SLIP ROLL",
                      "UP ECHO", "DOWN ECHO", "REV DELAY", "PAN", "CHORUS", "NOISE LFO", "PITCH",
                      "HPF", "LPF", "NOISE(CFX)", "GATE COMP", "BPF ECHO", "NOISE(RMX)", "SPIRAL UP",
                      "REVERB UP", "HPF ECHO", "LPF ECHO", "CRUSH ECHO", "SPIRAL DOWN", "REVERB DOWN"]
        for color in colors { result["PadFxColor\(color)"] = .effects }
        return result
    }()

    static let playKeys: Set<String> = Set((0...3).flatMap { deck in
        ["TempoRange", "VirtualDeckRpm", "Vinyl", "CrossFaderAssign", "BrakeValue", "StartValue"].map { "\($0)\(deck)" }
    })

    static let samplerKeys: Set<String> = ["visibleBankL", "quantize", "sync", "master_bpm"]

    static let browserScalarKeys: Set<String> = {
        var keys: Set<String> = ["ShowEditBrowser", "SearchButtonToggle_Main", "SearchButtonToggle_Sub",
                                 "TrafficLightKeyEnabled", "RelatedTracksKeySortColumnID", "RelatedTracksKeySortForward",
                                 "RelatedTracksKeyEnabledSelectedTree"]
        for filter in ["bpm", "key", "rate", "color", "mytag0", "mytag1", "mytag2", "mytag3"] {
            keys.insert("ColumnFilterToggle_\(filter)")
            keys.insert("ColumnFilterSelector_\(filter)")
        }
        return keys
    }()

    static func group(for key: String, in file: String, schemaVersion: Int = 2) -> SettingsGroup? {
        if file == mainFile {
            // Version 1 grouped some sampler settings under layout/performance.
            if schemaVersion == 1 {
                if ["SamplerPanelAutoOpen", "SamplerLayout", "SamplerColorType"].contains(key) { return .layout }
                if ["SamplerLoadLockAtLoadedSlot", "SlicerCaptureSaveBank", "ShouldLoopCaptureIfEmptySlot",
                    "SequenceLoadAutoPlay", "VelocityCurve", "SequencerQuantize", "SequencerQuantizeStatus"].contains(key) { return .performance }
            }
            return propertyGroups[key]
        }
        if file == playFile && playKeys.contains(key) { return .performance }
        if file == samplerFile {
            if samplerKeys.contains(key) { return .sampler }
            if key.hasPrefix("SamplerSet/"), samplerKeys.contains(String(key.dropFirst("SamplerSet/".count))) { return .sampler }
        }
        if file == browserFile && browserScalarKeys.contains(key) { return .layout }
        if file == grooveFile && (1...4).contains(where: { ["visibleBank\($0)", "singleMulti\($0)", "fxType\($0)"].contains(key) }) { return .groove }
        if file == videoFile && ["ImageOverlay/transparency", "ImageOverlay/size", "TextOverlay/transparency", "TextOverlay/size"].contains(key) { return .visuals }
        if file == "EditSettings.xml" && ["LoopMode", "ShowToolPanel", "SelectedPadSelectedTab"].contains(key) { return .layout }
        return nil
    }

    static let browserStructureKeys: Set<String> = {
        let contexts = ["TaglistTracks", "RequestCatalogTracks", "CollectionTracks", "CollectionTracks-sub",
                        "CollectionTracks-automix", "PlaylistTracks", "PlaylistTracks-sub", "PlaylistTracks-automix",
                        "SpotifyTracks", "FolderTracks", "RelatedTracks", "SamplerTracks", "DeviceTracks", "Recordings",
                        "DevicePlaylistTracks", "HistoryTracks", "SoundCloudTracks", "BeatportTracks", "TidalTracks", "AppleMusicTracks"]
        var keys: Set<String> = ["ArtworkStatus-TagList", "ArtworkStatus-TrackList"]
        for context in contexts {
            for suffix in ["", "-AttributeColumn", "-FullArtwork", "-FullArtwork-AttributeColumn"] {
                keys.insert("TableHeader-\(context)\(suffix)")
            }
        }
        return keys
    }()

    static func structureGroup(for key: String, in file: String) -> SettingsGroup? {
        if file == browserFile && browserStructureKeys.contains(key) { return .layout }
        if file == grooveFile && (1...4).map({ "FxSet\($0)" }).contains(key) { return .groove }
        return nil
    }

    static func groups(in file: String) -> Set<SettingsGroup> {
        switch file {
        case mainFile: return Set(propertyGroups.values)
        case playFile: return [.performance]
        case samplerFile: return [.sampler]
        case browserFile, "EditSettings.xml": return [.layout]
        case grooveFile: return [.groove]
        case videoFile: return [.visuals]
        default: return []
        }
    }

    static func validRelativePath(_ path: String) -> Bool {
        let parts = path.components(separatedBy: "/")
        return !path.isEmpty && path.utf8.count <= 512 && !path.contains("\\") &&
            !path.unicodeScalars.contains(where: { CharacterSet.controlCharacters.contains($0) }) &&
            parts.allSatisfy { !$0.isEmpty && $0 != "." && $0 != ".." && !$0.hasPrefix(".") }
    }

    static func fileGroup(_ path: String) -> SettingsGroup? {
        guard validRelativePath(path) else { return nil }
        if ["PadFxSettings.xml", "FxUnitSettings.xml", "FxUnitSettings6.xml", "FxUnitSettings7.xml", "MFXSettings.xml"].contains(path) {
            return .effects
        }
        let parts = path.components(separatedBy: "/")
        if parts.count == 2 && parts[0] == "KeyMappings" && parts[1].hasSuffix(".mappings") { return .mappings }
        if parts.count == 2 && parts[0] == "MidiMappings" && parts[1].hasSuffix(".midi.csv") { return .mappings }
        if parts.first == "pad" {
            if parts.count == 2 && (parts[1] == "Version" || parts[1].hasSuffix(".pad.csv")) { return .mappings }
            if parts.count == 3 && parts[1].range(of: "^assign[0-9]+$", options: .regularExpression) != nil && parts[2].hasSuffix(".pad.csv") {
                return .mappings
            }
        }
        return nil
    }
}

using System;
using System.Collections.Generic;

namespace RenPyVisualScriptMVVM.Modules.StoryEditor.Models;

public sealed record StoryTextFragmentEdit(
    Guid FragmentId,
    string SpeakerCode,
    string Text,
    bool UpdatesSpeaker = true,
    IReadOnlyList<string>? SegmentSpeakerCodes = null);

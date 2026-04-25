using System;

namespace RenPyVisualScriptMVVM.Modules.StoryEditor.Models;

public sealed record StoryTextFragmentEdit(Guid FragmentId, string SpeakerCode, string Text, bool UpdatesSpeaker = true);

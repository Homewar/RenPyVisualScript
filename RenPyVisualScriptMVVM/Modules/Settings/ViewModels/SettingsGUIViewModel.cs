using RenPyVisualScriptMVVM.Modules.Settings.Models;
using System;
using System.Collections.Generic;
using RenPyVisualScriptMVVM.Core.ViewModels;


namespace RenPyVisualScriptMVVM.Modules.Settings.ViewModels
{
	public class SettingsGUIViewModel : BaseViewModel
    {
		public GUISettings GUISettings { get; }

		public SettingsGUIViewModel()
		{
            GUISettings = new GUISettings
            {
			};
		}
	}
}
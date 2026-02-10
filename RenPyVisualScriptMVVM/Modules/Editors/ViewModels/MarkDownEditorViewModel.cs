using PropertyModels.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RenPyVisualScriptMVVM.Modules.Editors.ViewModels
{
	public class MarkDownEditorViewModel : ObservableObject
	{
		private string _MDText;
		public string MDText
		{ 
			get => _MDText;
			set => SetProperty(ref _MDText, value);
		}
    }
}
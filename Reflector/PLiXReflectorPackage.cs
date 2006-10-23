/**************************************************************************\
* Neumont PLiX (Programming Language in XML) Code Generator                *
*                                                                          *
* Copyright � Neumont University and Matthew Curland. All rights reserved. *
*                                                                          *
* The use and distribution terms for this software are covered by the      *
* Common Public License 1.0 (http://opensource.org/licenses/cpl) which     *
* can be found in the file CPL.txt at the root of this distribution.       *
* By using this software in any fashion, you are agreeing to be bound by   *
* the terms of this license.                                               *
*                                                                          *
* You must not remove this notice, or any other, from this software.       *
\**************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;
using Reflector;
using Reflector.CodeModel;

namespace Reflector
{
	/// <summary>
	/// Interface for providing PLiX configuration options
	/// </summary>
	public interface IPLiXConfiguration
	{
		/// <summary>
		/// Get the current example language. Can be null.
		/// </summary>
		ILanguage ExampleLanguage { get;}
	}
	/// <summary>
	/// Starting point for a Reflector class
	/// </summary>
	public sealed class PLiXLanguagePackage : IPackage
	{
		#region PLiXConfiguration class
		private sealed class PLiXConfiguration : IPLiXConfiguration
		{
			#region Member Variables
			private IConfiguration myConfiguration;
			private string myExampleLanguageName;
			private int myExampleLanguageIndex;
			private ILanguage myExampleLanguage;
			private PLiXLanguagePackage myPackage;
			#endregion // Member Variables
			#region Constants
			private const string ConfigurationSection = "PLiXLanguage";
			private const string ExampleLanguageValueName = "ExampleLanguage";
			#endregion // Constants
			#region Constructors
			/// <summary>
			/// Create a new PLiXConfiguration class
			/// </summary>
			/// <param name="package">The associated PLiXLanguagePackage</param>
			public PLiXConfiguration(PLiXLanguagePackage package)
			{
				myPackage = package;
				myConfiguration = ((IConfigurationManager)package.myServiceProvider.GetService(typeof(IConfigurationManager)))[ConfigurationSection];
				myExampleLanguageName = myConfiguration.GetProperty(ExampleLanguageValueName, "C#");
			}
			#endregion // Constructors
			#region IPLiXConfiguration Implementation
			public ILanguage ExampleLanguage
			{
				get
				{
					ILanguage retVal = myExampleLanguage;
					ILanguageCollection languages = myPackage.myLanguageManager.Languages;
					if (retVal == null)
					{
						string languageName = myExampleLanguageName;
						if (!string.IsNullOrEmpty(languageName))
						{
							int languageCount = languages.Count;
							for (int i = 0; i < languageCount; ++i)
							{
								ILanguage testLanguage = languages[i];
								if (testLanguage.Name == languageName)
								{
									myExampleLanguage = retVal = testLanguage;
									myExampleLanguageIndex = i;
									break;
								}
							}
							if (retVal == null)
							{
								myConfiguration.SetProperty(ExampleLanguageValueName, "");
							}
						}
					}
					else
					{
						// There is no notification when this set changes, so make a sanity
						// check by verifying that the language at the recorded index has not changed.
						int languageCount = languages.Count;
						int testIndex = myExampleLanguageIndex;
						if (!(testIndex < languageCount && languages[testIndex] == retVal))
						{
							int i = 0;
							for (; i < languageCount; ++i)
							{
								if (languages[i] == retVal)
								{
									myExampleLanguageIndex = i;
									break;
								}
							}
							if (i == languageCount)
							{
								myExampleLanguageName = "";
								myExampleLanguage = retVal = null;
							}
						}
					}
					return retVal;
				}
				set
				{
					if (value != ExampleLanguage)
					{
						myExampleLanguage = value;
						string languageName = (value != null) ? value.Name : "";
						myExampleLanguageName = languageName;
						myConfiguration.SetProperty(ExampleLanguageValueName, languageName);
						// Forces rerendering of the selected item
						myPackage.myLanguageManager.ActiveLanguage = myPackage.myLanguageManager.ActiveLanguage;
						// myExampleLanguageIndex will refresh itself automatically
					}
				}
			}
			#endregion // IPLiXConfiguration Implementation
		}
		#endregion // PLiXConfiguraiton class
		#region Member Variables
		private ILanguageManager myLanguageManager;
		private ILanguage myLanguage;
		private IServiceProvider myServiceProvider;
		private ICommandBarMenu myTopMenu;
		private ICommandBarMenu myExampleLanguageMenu;
		private IPLiXConfiguration myConfiguration;
		#endregion // Member Variables
		#region IPackage Implementation
		void IPackage.Load(IServiceProvider serviceProvider)
		{
			// Set this early so it is easily referenced
			myServiceProvider = serviceProvider;

			ILanguageManager languageManager = (ILanguageManager)serviceProvider.GetService(typeof(ILanguageManager));
			myLanguageManager = languageManager;
			myConfiguration = new PLiXConfiguration(this);
			languageManager.ActiveLanguageChanged += new EventHandler(OnActiveLanguageChanged);
			ILanguage language = new PLiXLanguage((ITranslatorManager)serviceProvider.GetService(typeof(ITranslatorManager)), myConfiguration);
			languageManager.RegisterLanguage(language);
			myLanguage = language;

			// Add our PLiX menu item, activated when the plix language is active
			ICommandBarManager commandBarManager = (ICommandBarManager)serviceProvider.GetService(typeof(ICommandBarManager));
			ICommandBar menuBar = commandBarManager.CommandBars["MenuBar"];
			ICommandBarMenu topMenu = menuBar.Items.InsertMenu(menuBar.Items.Count - 1, "PLiXLanguageOptions", "PLi&X");
			topMenu.Visible = false;
			topMenu.DropDown += new EventHandler(OnOpenTopMenu);

			ICommandBarMenu exampleLanguageMenu = topMenu.Items.AddMenu("PLiXExampleLanguage", "&Example Language");
			myExampleLanguageMenu = exampleLanguageMenu;
			myTopMenu = topMenu;
		}

		void IPackage.Unload()
		{
			myLanguageManager.ActiveLanguageChanged -= new EventHandler(OnActiveLanguageChanged);
			myTopMenu.DropDown -= new EventHandler(OnOpenTopMenu);
			myLanguageManager.UnregisterLanguage(myLanguage);
			((ICommandBarManager)myServiceProvider.GetService(typeof(ICommandBarManager))).CommandBars["MenuBar"].Items.Remove(myTopMenu);
		}
		/// <summary>
		/// Event handler to show our PLiX menu when we're the active rendering language
		/// </summary>
		private void OnActiveLanguageChanged(object sender, EventArgs e)
		{
			myTopMenu.Visible = myLanguageManager.ActiveLanguage == myLanguage;
		}
		/// <summary>
		/// Synchronize the example language sub menu with the current set of languages.
		/// Unfortunately, there is no add/remove event when languages are added and removed,
		/// and the DropDown event does not fire on submenus, so we need to synchronize here.
		/// </summary>
		void OnOpenTopMenu(object sender, EventArgs e)
		{
			ICommandBarItemCollection exampleItems = myExampleLanguageMenu.Items;
			ILanguageCollection languages = myLanguageManager.Languages;
			ILanguage selectedLanguage = myConfiguration.ExampleLanguage;
			int itemsCount = exampleItems.Count;
			ICommandBarCheckBox currentItem;
			if (itemsCount == 0)
			{
				currentItem = exampleItems.AddCheckBox("None");
				currentItem.Checked = selectedLanguage == null;
				currentItem.Click += new EventHandler(OnExampleLanguageClick);
				++itemsCount;
			}
			else
			{
				currentItem = (ICommandBarCheckBox)exampleItems[0];
				if (currentItem.Checked ^ (selectedLanguage == null))
				{
					currentItem.Checked = selectedLanguage == null;
				}
			}

			int languagesCount = languages.Count;
			int currentItemIndex = 1; // None is at the zero position
			for (int iLanguage = 0; iLanguage < languagesCount; ++iLanguage)
			{
				ILanguage currentLanguage = languages[iLanguage];
				bool isChecked = currentLanguage == selectedLanguage;
				string languageName = currentLanguage.Name;
				if (currentLanguage != myLanguage && languageName != "IL")
				{
					if (currentItemIndex >= itemsCount)
					{
						currentItem = exampleItems.AddCheckBox(languageName);
						currentItem.Value = currentLanguage;
						if (isChecked)
						{
							currentItem.Checked = true;
						}
						currentItem.Click += new EventHandler(OnExampleLanguageClick);
						// No need to adjust currentItemIndex here, we'll continue to add to the end of the list
					}
					else
					{
						currentItem = (ICommandBarCheckBox)exampleItems[currentItemIndex];
						ILanguage testLanguage = (ILanguage)currentItem.Value;
						if (testLanguage == currentLanguage)
						{
							++currentItemIndex;
							if (currentItem.Checked ^ isChecked)
							{
								currentItem.Checked = isChecked;
							}
							continue;
						}
						else
						{
							// If the testLanguage appears later in the language list, then we need
							// to insert the new language here. Otherwise, we need to remove the
							// existing item.
							int matchingLanguage = iLanguage + 1;
							for (; matchingLanguage < languagesCount; ++matchingLanguage)
							{
								if (testLanguage == languages[matchingLanguage])
								{
									break;
								}
							}
							if (matchingLanguage < languagesCount)
							{
								// The language at this item will match later. Insert the currentLanguage at this position
								currentItem = exampleItems.InsertCheckBox(currentItemIndex, languageName);
								currentItem.Value = currentLanguage;
								if (isChecked)
								{
									currentItem.Checked = true;
								}
								currentItem.Click += new EventHandler(OnExampleLanguageClick);
								++itemsCount;
								++currentItemIndex;
							}
							else
							{
								// The item needs to be removed
								exampleItems.Remove(currentItem);
								--itemsCount;
							}
						}
					}
				}
			}
			// Remove any remaining items that we didn't match with a language
			if (currentItemIndex < itemsCount)
			{
				for (int i = itemsCount - 1; i >= currentItemIndex; --i)
				{
					exampleItems.RemoveAt(i);
				}
			}
		}
		/// <summary>
		/// Event handler for clicking a different example language
		/// </summary>
		void OnExampleLanguageClick(object sender, EventArgs e)
		{
			((PLiXConfiguration)myConfiguration).ExampleLanguage = ((ICommandBarItem)sender).Value as ILanguage;
		}
		#endregion // IPackage Implementation
	}
}

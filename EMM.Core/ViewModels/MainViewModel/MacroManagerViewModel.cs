﻿using Data;
using EMM.Core.Converter;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace EMM.Core.ViewModels
{
    /// <summary>
    /// This class is used to manage macro: new, load, save....
    /// </summary>
    public class MacroManagerViewModel : BaseViewModel
    {
        #region Ctor

        public MacroManagerViewModel(DataIO dataIO, IMessageBoxService messageBoxService, ViewModelFactory viewModelFactory)
        {
            this.dataIO = dataIO;
            this.messageBoxService = messageBoxService;
            this.viewModelFactory = viewModelFactory;

            //var test = new TestClass();

            //CurrentMacro = this.LoadMacroViewModel(test.ReturnTestTemplate());

            InitializeCommands();
            HookEventHandler();
        }

        #endregion

        #region Private members

        private DataIO dataIO;
        private IMessageBoxService messageBoxService;
        private ViewModelFactory viewModelFactory;

        private MacroViewModel currentMacro;
        private int errorCount;

        #endregion

        #region Public Properties

        /// <summary>
        /// Check if macro is loaded
        /// </summary>
        public bool IsMacroLoaded => (CurrentMacro == null) ? false : true;

        /// <summary>
        /// Current Macro ViewModel ready to be edited....
        /// </summary>
        public MacroViewModel CurrentMacro
        {
            get => this.currentMacro;
            set
            {
                this.currentMacro = value;
                this.CurrentMacroChanged?.Invoke(this, null);
            }
        }

        #endregion

        #region Commands

        public ICommand NewCommand { get; set; }
        public ICommand OpenCommand { get; set; }
        public ICommand ExitCommand { get; set; }
        public ICommand SaveCommand { get; set; }
        public ICommand SaveAsCommand { get; set; }

        private void InitializeCommands()
        {
            NewCommand = new RelayCommand(async p =>
            {
                if (await ShouldSaveMacro() == null)
                    return;

                CurrentMacro = viewModelFactory.NewMacroViewModel();
            });

            OpenCommand = new RelayCommand(async p =>
            {
                if (await ShouldSaveMacro() == null)
                    return;

                var loadedMacro = await Task.Run(() => this.LoadMacroViewModel());

                if (loadedMacro == null)
                {
                    return;
                }

                this.CurrentMacro = loadedMacro;
            });

            ExitCommand = new RelayCommand(p => Application.Current.Shutdown());

            SaveCommand = new RelayCommand(async p =>
            {
                this.CurrentMacro.MacroPath = await Task.Run(() => this.SaveMacro(CurrentMacro, this.CurrentMacro.MacroPath));
                CurrentMacro.AcceptChanges();
            }, p => CurrentMacro != null);

            SaveAsCommand = new RelayCommand(async p =>
            {
                this.CurrentMacro.MacroPath = await Task.Run(() => this.SaveAsMacro(CurrentMacro, this.CurrentMacro.MacroPath));
                CurrentMacro.AcceptChanges();
            }, p => CurrentMacro != null);
        }
       
        #endregion

        #region Events

        public event EventHandler CurrentMacroChanged;

        private void HookEventHandler()
        {
            //Handler file drop
            Messenger.Register(async (sender, e) =>
            {
                if (await ShouldSaveMacro() == null)
                    return;

                if (e.FilePaths == null || e.FilePaths.Length < 1)
                    return;

                var filePath = e.FilePaths[0];

                this.SetCurrentMacro(filePath);
            });

            //SaveMacroBeforeExit
            Messenger.Register((sender, e) =>
            {
                if (e.EventMessage != EventMessage.SaveMacroBeforeExit)
                    return;

                this.SaveCommand.Execute(null);
            });
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Set the current macro to the pass in macro
        /// </summary>
        /// <param name="macro"></param>
        public void SetCurrentMacro(MacroViewModel macro)
        {
            this.CurrentMacro = macro;
        }

        /// <summary>
        /// set the current macro base on the path
        /// </summary>
        /// <param name="macro"></param>
        public void SetCurrentMacro(string path)
        {
            var loadedMacro = this.LoadMacroViewModel(path);
            if (loadedMacro == null)
                return;

            this.CurrentMacro = loadedMacro;
            this.CurrentMacro?.AcceptChanges();
        }

        public MacroViewModel GetCurrentMacro()
        {
            return this.CurrentMacro;
        }

        /// <summary>
        /// Get the currently selected action, return null if no selection
        /// </summary>
        /// <returns></returns>
        public IActionViewModel GetCurrentSelectedActionViewModel()
        {
            return (this.CurrentMacro?.SelectedItem as ActionGroupViewModel)?.SelectedItem;
        }

        /// <summary>
        /// Load <see cref="MacroViewModel"/> from <see cref="MacroTemplate"/>
        /// </summary>
        /// <param name="macroTemplate">The source <see cref="MacroTemplate"/></param>
        /// <returns></returns>
        public MacroViewModel LoadMacroViewModel(MacroTemplate macroTemplate)
        {
            return viewModelFactory.NewMacroViewModel().PopulateProperties(macroTemplate);
        }

        /// <summary>
        /// Load <see cref="MacroViewModel"/> from path
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public MacroViewModel LoadMacroViewModel(string path)
        {
            this.errorCount = 0;
            var loadedMacro = this.dataIO.LoadMacroFileFromPath(path, ErrorCallBack);
            this.CheckError();

            if (loadedMacro == null)
            {
                this.messageBoxService.ShowMessageBox("Cannot parse the file", "ERROR", MessageButton.OK, MessageImage.Error);
                return null;
            }

            var vm = viewModelFactory.NewMacroViewModel().PopulateProperties(loadedMacro.MacroTemplate);
            vm.MacroPath = path;
            return vm;
        }

        /// <summary>
        /// Load <see cref="MacroViewModel"/> from file using OpenFileDialog
        /// </summary>
        /// <returns></returns>
        public MacroViewModel LoadMacroViewModel()
        {
            this.errorCount = 0;
            var loadedMacro = this.dataIO.LoadFromFile(null, ErrorCallBack);
            this.CheckError();

            if (loadedMacro == null && errorCount == 0) //User press cancel
                return null;

            if (loadedMacro == null)
            {
                this.messageBoxService.ShowMessageBox("Cannot parse the file", "ERROR", MessageButton.OK, MessageImage.Error);
                return null;
            }

            var macroViewModel = this.LoadMacroViewModel(loadedMacro.MacroTemplate);

            macroViewModel.MacroPath = loadedMacro.MacroFullPath;

            macroViewModel.AcceptChanges();

            return macroViewModel;
        }

        /// <summary>
        /// Save the Macro to file
        /// </summary>
        /// <param name="macroViewModel">The Macro to be saved</param>
        private string SaveMacro(MacroViewModel macroViewModel, string fullPath)
        {
            var macroTemplate = macroViewModel.ConvertBack();

            return this.dataIO.SaveToFile(macroTemplate, fullPath);
        }

        /// <summary>
        /// Save AS the Macro to file
        /// </summary>
        /// <param name="macroViewModel">The Macro to be saved</param>
        private string SaveAsMacro(MacroViewModel macroViewModel, string fullPath)
        {
            var macroTemplate = macroViewModel.ConvertBack();

            return this.dataIO.SaveAsToFile(macroTemplate, fullPath);
        }

        /// <summary>
        /// Ask for saving macro if the macro's IsChanged is true
        /// </summary>
        private async Task<bool?> ShouldSaveMacro()
        {
            if (CurrentMacro == null)
                return false;

            if (CurrentMacro.IsChanged == true)
            {
                MessageResult result = MessageResult.Cancel;
                if (CurrentMacro != null)
                {
                    result = await Task.Run(() => this.messageBoxService.ShowMessageBox("Do you want to save the current Macro?", "EMM", MessageButton.YesNoCancel, MessageImage.Question));
                }

                switch (result)
                {
                    case MessageResult.Cancel:
                    case MessageResult.None:
                        return null;
                    case MessageResult.No:
                        return false;
                    case MessageResult.Yes:
                        SaveCommand.Execute(null);
                        return true;
                }
            }

            return false;
        }

        private void ErrorCallBack(Newtonsoft.Json.Serialization.ErrorEventArgs e)
        {
            this.errorCount++;
        }
        private void CheckError()
        {
            if (errorCount > 0)
            {
                this.messageBoxService.ShowMessageBox(this.errorCount + " error(s) occur while trying to parse the file. The parsed macro might not be completed", "ERROR", MessageButton.OK, MessageImage.Error);
                return;
            }
        }


        #endregion
    }
}

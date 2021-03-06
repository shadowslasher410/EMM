﻿using Data;
using EMM.Core;
using EMM.Core.Converter;
using EMM.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AEMG_EX.Core
{
    public class AEScriptGenerator
    {
        public AEScriptGenerator(ScriptApplyFactory scriptApplyFactory, AESettingViewModel setting, IMessageBoxService messageBoxService, SimpleAutoMapper autoMapper)
        {
            this.scriptApplyFactory = scriptApplyFactory;
            this.setting = setting;
            this.messageBoxService = messageBoxService;
            this.autoMapper = autoMapper;
        }

        private ScriptApplyFactory scriptApplyFactory;

        private AESettingViewModel setting;

        private IMessageBoxService messageBoxService;

        private SimpleAutoMapper autoMapper;

        /// <summary>
        /// Generate script
        /// </summary>
        /// <param name="macro">The macro template</param>
        /// <param name="aEActionList">AEAction list from user choice</param>
        /// <returns></returns>
        public bool? GenerateScript(MacroTemplate macro, IList<IAEActionViewModel> aEActionList)
        { 
            var timer = 200;

            if (!ApplyConvertSetting(macro))
                return false;

            var macroTemplate = this.ConstructCompleteMacro(macro, aEActionList);

            var script = macroTemplate.GenerateScript(ref timer);

            return scriptApplyFactory.GetScriptApplier(setting.SelectedEmulator).ApplyScriptTo(string.IsNullOrEmpty(setting.CustomName) ? macroTemplate.MacroName : setting.CustomName, setting.SelectedPath, script);

        }

        /// <summary>
        /// Generate script for the selected AEaction only
        /// </summary>
        /// <param name="macro">The macro template</param>
        /// <param name="aEActionList">the aeaction to generate script</param>
        /// <returns></returns>
        public bool? GenerateScript(MacroTemplate macro, IAEActionViewModel aEAction)
        {
            var timer = 200;

            if (!ApplyConvertSetting(macro))
                return false;

            var actionList = ScaleActionsToMacroResolution(macro, aEAction.UserChoicesToActionList());

            var script = new StringBuilder();

            foreach (var action in actionList)
            {
                script.Append(action.GenerateAction(ref timer));
            }

            return scriptApplyFactory.GetScriptApplier(setting.SelectedEmulator).ApplyScriptTo(string.IsNullOrEmpty(setting.CustomName) ? macro.MacroName + "_Test": setting.CustomName + "_Test", setting.SelectedPath, script, false);
        }

        private MacroTemplate ConstructCompleteMacro(MacroTemplate template, IList<IAEActionViewModel> aEActionList)
        {
            //Copy the macro so subsequence convert start on fresh macro
            var copiedMacro = this.autoMapper.SimpleAutoMap<MacroTemplate, MacroTemplate>(template);

            copiedMacro.ActionGroupList = copiedMacro.ActionGroupList.Select(ag => (IAction)new ActionGroup { Repeat = (ag as ActionGroup).Repeat, ActionList = new List<IAction>((ag as ActionGroup).ActionList) } ).ToList();

            var offset = 0;
            var lastActionGroupIndex = -1;

            foreach (var action in aEActionList)
            {
                var actionList = ScaleActionsToMacroResolution(template, action.UserChoicesToActionList());
                var actionIndexToInsert = action.ActionIndex;

                //Fix index to insert if placeholder in the same actiongroup
                if (action.ActionGroupIndex == lastActionGroupIndex)
                {
                    actionIndexToInsert += offset;
                    offset += actionList.Count;
                }
                else
                {
                    offset = actionList.Count;
                }

                (copiedMacro.ActionGroupList[action.ActionGroupIndex] as ActionGroup).ActionList.InsertRange(actionIndexToInsert, actionList);

                lastActionGroupIndex = action.ActionGroupIndex;
            }

            return copiedMacro;
        }

        private bool ApplyConvertSetting(MacroTemplate macro)
        {
            GlobalData.CustomX = setting.CustomX;
            GlobalData.CustomY = setting.CustomY;
            GlobalData.Emulator = setting.SelectedEmulator;
            GlobalData.Randomize = setting.Randomize;

            try
            {
                GlobalData.ScaleX = (double)setting.CustomX / macro.OriginalX;
                GlobalData.ScaleY = (double)setting.CustomY / macro.OriginalY;
            }
            catch
            {
                messageBoxService.ShowMessageBox("The macro doesnt have any resolution set. Please set in in EMM", "Error", MessageButton.OK, MessageImage.Error);
                return false;
            }

            return true;
        }

        private IList<IAction> ScaleActionsToMacroResolution(MacroTemplate macro, IList<IAction> actionList)
        {
            var newList = new List<IAction>();
            double scaleX = macro.OriginalX / 1280.0;
            double scaleY = macro.OriginalY / 720.0;

            foreach (var action in actionList)
            {
                switch (action.BasicAction)
                {
                    case BasicAction.Click:
                        var copied = this.autoMapper.SimpleAutoMap<Click, Click>(action as Click);
                        copied.ClickPoint = new System.Windows.Point(Math.Round((action as Click).ClickPoint.X * scaleX), Math.Round((action as Click).ClickPoint.Y * scaleY));
                        newList.Add(copied);
                        break;
                    case BasicAction.Swipe:
                        var copiedSwipe = this.autoMapper.SimpleAutoMap<Swipe, Swipe>(action as Swipe);
                        copiedSwipe.PointList = copiedSwipe.PointList.Select(sp => new SwipePoint
                        {
                            HoldTime = sp.HoldTime,
                            SwipeSpeed = sp.SwipeSpeed,
                            Point = new System.Windows.Point(Math.Round(sp.Point.X * scaleX), Math.Round(sp.Point.Y * scaleY))
                        }).ToList();
                        newList.Add(copiedSwipe);
                        break;
                    default:
                        newList.Add(action);
                        break;
                }
            }

            return newList;
        }
    }
}

﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using GitCommands;
using GitCommands.Git.Commands;
using GitCommands.Patches;
using GitExtUtils.GitUI.Theming;
using GitUI.HelperDialogs;
using ResourceManager;

namespace GitUI.CommandsDialogs
{
    public partial class FormRebase : GitModuleForm
    {
        private static readonly List<PatchFile> Skipped = new List<PatchFile>();
        private readonly TranslationString _continueRebaseText = new("Continue rebase");
        private readonly TranslationString _solveConflictsText = new("Solve conflicts");

        private readonly TranslationString _solveConflictsText2 = new(">Solve conflicts<");
        private readonly TranslationString _continueRebaseText2 = new(">Continue rebase<");

        private readonly TranslationString _noBranchSelectedText = new("Please select a branch");

        private readonly TranslationString _branchUpToDateText =
            new TranslationString("Current branch a is up to date." + Environment.NewLine + "Nothing to rebase.");
        private readonly TranslationString _branchUpToDateCaption = new("Rebase");

        private readonly TranslationString _hoverShowImageLabelText = new("Hover to see scenario when fast forward is possible.");

        private readonly string? _defaultBranch;
        private readonly string? _defaultToBranch;
        private readonly bool _startRebaseImmediately;

        [Obsolete("For VS designer and translation test only. Do not remove.")]
        private FormRebase()
        {
            InitializeComponent();
        }

        public FormRebase(GitUICommands commands, string? defaultBranch)
            : base(commands)
        {
            _defaultBranch = defaultBranch;
            InitializeComponent();
            SolveMergeconflicts.BackColor = OtherColors.MergeConflictsColor;
            SolveMergeconflicts.SetForeColorForBackColor();
            helpImageDisplayUserControl1.Image1 = Properties.Images.HelpCommandRebase.AdaptLightness();
            InitializeComplete();
            helpImageDisplayUserControl1.Visible = !AppSettings.DontShowHelpImages;
            helpImageDisplayUserControl1.IsOnHoverShowImage2NoticeText = _hoverShowImageLabelText.Text;
            patchGrid1.SetSkipped(Skipped);
            if (AppSettings.AlwaysShowAdvOpt)
            {
                ShowOptions_LinkClicked(this, null!);
            }

            Shown += FormRebase_Shown;
        }

        private void FormRebase_Shown(object sender, EventArgs e)
        {
            patchGrid1.SelectCurrentlyApplyingPatch();
        }

        public FormRebase(GitUICommands commands, string? from, string? to, string? defaultBranch, bool interactive = false, bool startRebaseImmediately = true)
            : this(commands, defaultBranch)
        {
            txtFrom.Text = from;
            _defaultToBranch = to;
            chkInteractive.Checked = interactive;
            chkAutosquash.Enabled = interactive;
            _startRebaseImmediately = startRebaseImmediately;
        }

        private void FormRebaseLoad(object sender, EventArgs e)
        {
            var selectedHead = Module.GetSelectedBranch();
            Currentbranch.Text = selectedHead;

            var refs = Module.GetRefs(true, true).OfType<GitRef>().ToList();
            Branches.DataSource = refs;
            Branches.DisplayMember = nameof(GitRef.Name);

            if (_defaultBranch is not null)
            {
                Branches.Text = _defaultBranch;
            }

            Branches.Select();

            refs = Module.GetRefs(false, true).OfType<GitRef>().ToList();
            cboTo.DataSource = refs;
            cboTo.DisplayMember = nameof(GitRef.Name);

            cboTo.Text = _defaultToBranch ?? selectedHead;

            rebasePanel.Visible = !Module.InTheMiddleOfRebase();
            EnableButtons();

            // Honor the rebase.autosquash configuration.
            var autosquashSetting = Module.GetEffectiveSetting("rebase.autosquash");
            chkAutosquash.Checked = autosquashSetting.Trim().ToLower() == "true";

            chkStash.Checked = AppSettings.RebaseAutoStash;
            if (_startRebaseImmediately)
            {
                OkClick(this, EventArgs.Empty);
            }
            else
            {
                ShowOptions_LinkClicked(this, null!);
            }
        }

        private void EnableButtons()
        {
            if (Module.InTheMiddleOfRebase())
            {
                if (Height < 200)
                {
                    Height = 500;
                }

                Branches.Enabled = false;
                Ok.Enabled = false;
                chkStash.Enabled = false;

                AddFiles.Enabled = true;
                Commit.Enabled = true;
                Resolved.Enabled = !Module.InTheMiddleOfConflictedMerge();
                Mergetool.Enabled = Module.InTheMiddleOfConflictedMerge();
                Skip.Enabled = true;
                Abort.Enabled = true;
            }
            else
            {
                Branches.Enabled = true;
                Ok.Enabled = true;
                AddFiles.Enabled = false;
                Commit.Enabled = false;
                Resolved.Enabled = false;
                Mergetool.Enabled = false;
                Skip.Enabled = false;
                Abort.Enabled = false;
                chkStash.Enabled = Module.IsDirtyDir();
            }

            SolveMergeconflicts.Visible = Module.InTheMiddleOfConflictedMerge();

            Resolved.Text = _continueRebaseText.Text;
            Mergetool.Text = _solveConflictsText.Text;
            Resolved.ForeColor = SystemColors.ControlText;
            Mergetool.ForeColor = SystemColors.ControlText;
            ContinuePanel.BackColor = Color.Transparent;
            MergeToolPanel.BackColor = Color.Transparent;

            var highlightColor = Color.Yellow.AdaptBackColor();

            if (Module.InTheMiddleOfConflictedMerge())
            {
                AcceptButton = Mergetool;
                Mergetool.Focus();
                Mergetool.Text = _solveConflictsText2.Text;
                MergeToolPanel.BackColor = highlightColor;
            }
            else if (Module.InTheMiddleOfRebase())
            {
                AcceptButton = Resolved;
                Resolved.Focus();
                Resolved.Text = _continueRebaseText2.Text;
                ContinuePanel.BackColor = highlightColor;
            }
        }

        private void MergetoolClick(object sender, EventArgs e)
        {
            UICommands.StartResolveConflictsDialog(this);
            EnableButtons();
        }

        private void chkInteractive_CheckedChanged(object sender, EventArgs e)
        {
            chkAutosquash.Enabled = chkInteractive.Checked;
        }

        private void chkIgnoreDate_CheckedChanged(object sender, EventArgs e)
        {
            ToggleDateCheckboxMutualExclusions();
        }

        private void chkCommitterDateIsAuthorDate_CheckedChanged(object sender, EventArgs e)
        {
            ToggleDateCheckboxMutualExclusions();
        }

        private void ToggleDateCheckboxMutualExclusions()
        {
            chkCommitterDateIsAuthorDate.Enabled = !chkIgnoreDate.Checked;
            chkIgnoreDate.Enabled = !chkCommitterDateIsAuthorDate.Checked;
            chkInteractive.Enabled = !chkIgnoreDate.Checked && !chkCommitterDateIsAuthorDate.Checked;
            chkPreserveMerges.Enabled = !chkIgnoreDate.Checked && !chkCommitterDateIsAuthorDate.Checked;
            chkAutosquash.Enabled = chkInteractive.Checked && !chkIgnoreDate.Checked && !chkCommitterDateIsAuthorDate.Checked;
        }

        private void AddFilesClick(object sender, EventArgs e)
        {
            UICommands.StartAddFilesDialog(this);
        }

        private void ResolvedClick(object sender, EventArgs e)
        {
            using (WaitCursorScope.Enter())
            {
                FormProcess.ShowDialog(this, process: null, arguments: GitCommandHelpers.ContinueRebaseCmd(), Module.WorkingDir, input: null, useDialogSettings: true);

                if (!Module.InTheMiddleOfRebase())
                {
                    Close();
                }

                EnableButtons();
                patchGrid1.Initialize();
            }
        }

        private void SkipClick(object sender, EventArgs e)
        {
            using (WaitCursorScope.Enter())
            {
                var applyingPatch = patchGrid1.PatchFiles.FirstOrDefault(p => p.IsNext);
                if (applyingPatch is not null)
                {
                    applyingPatch.IsSkipped = true;
                    Skipped.Add(applyingPatch);
                }

                FormProcess.ShowDialog(this, process: null, arguments: GitCommandHelpers.SkipRebaseCmd(), Module.WorkingDir, input: null, useDialogSettings: true);

                if (!Module.InTheMiddleOfRebase())
                {
                    Close();
                }

                EnableButtons();

                patchGrid1.RefreshGrid();
            }
        }

        private void AbortClick(object sender, EventArgs e)
        {
            using (WaitCursorScope.Enter())
            {
                FormProcess.ShowDialog(this, process: null, arguments: GitCommandHelpers.AbortRebaseCmd(), Module.WorkingDir, input: null, useDialogSettings: true);

                if (!Module.InTheMiddleOfRebase())
                {
                    Skipped.Clear();
                    Close();
                }

                EnableButtons();
                patchGrid1.Initialize();
            }
        }

        private void OkClick(object sender, EventArgs e)
        {
            using (WaitCursorScope.Enter())
            {
                if (string.IsNullOrEmpty(Branches.Text))
                {
                    MessageBox.Show(this, _noBranchSelectedText.Text, TranslatedStrings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                AppSettings.RebaseAutoStash = chkStash.Checked;

                Skipped.Clear();

                string rebaseCmd;
                if (chkSpecificRange.Checked && !string.IsNullOrWhiteSpace(txtFrom.Text) && !string.IsNullOrWhiteSpace(cboTo.Text))
                {
                    rebaseCmd = GitCommandHelpers.RebaseCmd(
                        cboTo.Text, chkInteractive.Checked, chkPreserveMerges.Checked,
                        chkAutosquash.Checked, chkStash.Checked, chkIgnoreDate.Checked, chkCommitterDateIsAuthorDate.Checked, txtFrom.Text, Branches.Text);
                }
                else
                {
                    rebaseCmd = GitCommandHelpers.RebaseCmd(
                        Branches.Text, chkInteractive.Checked,
                        chkPreserveMerges.Checked, chkAutosquash.Checked, chkStash.Checked, chkIgnoreDate.Checked, chkCommitterDateIsAuthorDate.Checked);
                }

                string cmdOutput = FormProcess.ReadDialog(this, process: null, arguments: rebaseCmd, Module.WorkingDir, input: null, useDialogSettings: true);
                if (cmdOutput.Trim() == "Current branch a is up to date.")
                {
                    MessageBox.Show(this, _branchUpToDateText.Text, _branchUpToDateCaption.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                if (!Module.InTheMiddleOfAction() &&
                    !Module.InTheMiddleOfPatch())
                {
                    Close();
                }

                EnableButtons();
                patchGrid1.Initialize();
            }
        }

        private void SolveMergeConflictsClick(object sender, EventArgs e)
        {
            MergetoolClick(sender, e);
        }

        private void ShowOptions_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            ShowOptions.Visible = false;
            OptionsPanel.Visible = true;
        }

        private void chkUseFromOnto_CheckedChanged(object sender, EventArgs e)
        {
            txtFrom.Enabled = chkSpecificRange.Checked;
            cboTo.Enabled = chkSpecificRange.Checked;
            btnChooseFromRevision.Enabled = chkSpecificRange.Checked;
        }

        private void btnChooseFromRevision_Click(object sender, EventArgs e)
        {
            using var chooseForm = new FormChooseCommit(UICommands, txtFrom.Text);
            if (chooseForm.ShowDialog(this) == DialogResult.OK && chooseForm.SelectedRevision is not null)
            {
                txtFrom.Text = chooseForm.SelectedRevision.ObjectId.ToShortString();
            }
        }

        private void Commit_Click(object sender, EventArgs e)
        {
            UICommands.StartCommitDialog(this);
            EnableButtons();
        }

        internal TestAccessor GetTestAccessor() => new TestAccessor(this);

        internal readonly struct TestAccessor
        {
            private readonly FormRebase _form;

            public TestAccessor(FormRebase form)
            {
                _form = form;
            }

            public CheckBox chkInteractive => _form.chkInteractive;
            public CheckBox chkPreserveMerges => _form.chkPreserveMerges;
            public CheckBox chkAutosquash => _form.chkAutosquash;
            public CheckBox chkStash => _form.chkStash;
            public CheckBox chkIgnoreDate => _form.chkIgnoreDate;
            public CheckBox chkCommitterDateIsAuthorDate => _form.chkCommitterDateIsAuthorDate;
        }
    }
}

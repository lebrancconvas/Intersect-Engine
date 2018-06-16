﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using DarkUI.Controls;
using DarkUI.Forms;
using Intersect.Editor.ContentManagement;
using Intersect.Editor.Core;
using Intersect.Editor.General;
using Intersect.Editor.Localization;
using Intersect.Editor.Networking;
using Intersect.Enums;
using Intersect.GameObjects;
using Intersect.Utilities;
using Microsoft.Xna.Framework.Graphics;

namespace Intersect.Editor.Forms.Editors
{
    public partial class FrmAnimation : EditorForm
    {
        private List<AnimationBase> mChanged = new List<AnimationBase>();
        private string mCopiedItem;
        private AnimationBase mEditorItem;

        private int mLowerFrame;

        private bool mPlayLower;
        private bool mPlayUpper;
        private int mUpperFrame;
        private RenderTarget2D mLowerDarkness;

        //Mono Rendering Variables
        private SwapChainRenderTarget mLowerWindow;

        private RenderTarget2D mUpperDarkness;
        private SwapChainRenderTarget mUpperWindow;

        public FrmAnimation()
        {
            ApplyHooks();
            InitializeComponent();
            lstAnimations.LostFocus += itemList_FocusChanged;
            lstAnimations.GotFocus += itemList_FocusChanged;
        }

        protected override void GameObjectUpdatedDelegate(GameObjectType type)
        {
            if (type != GameObjectType.Animation) return;
            InitEditor();
            if (mEditorItem == null || AnimationBase.Lookup.Values.Contains(mEditorItem)) return;
            mEditorItem = null;
            UpdateEditor();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            foreach (var item in mChanged)
            {
                item.RestoreBackup();
                item.DeleteBackup();
            }

            Hide();
            Globals.CurrentEditor = -1;
            Dispose();
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            //Send Changed items
            foreach (var item in mChanged)
            {
                PacketSender.SendSaveObject(item);
                item.DeleteBackup();
            }

            Hide();
            Globals.CurrentEditor = -1;
            Dispose();
        }

        private void lstAnimations_Click(object sender, EventArgs e)
        {
            if (mChangingName) return;
            mEditorItem =
                AnimationBase.Lookup.Get<AnimationBase>(
                    Database.GameObjectIdFromList(GameObjectType.Animation, lstAnimations.SelectedIndex));
            UpdateEditor();
        }

        private void frmAnimation_Load(object sender, EventArgs e)
        {
            //Animation Sound
            cmbSound.Items.Clear();
            cmbSound.Items.Add(Strings.General.none);
            cmbSound.Items.AddRange(GameContentManager.SmartSortedSoundNames);

            //Lower Animation Graphic
            cmbLowerGraphic.Items.Clear();
            cmbLowerGraphic.Items.Add(Strings.General.none);
            cmbLowerGraphic.Items.AddRange(
                GameContentManager.GetSmartSortedTextureNames(GameContentManager.TextureType.Animation));

            //Upper Animation Graphic
            cmbUpperGraphic.Items.Clear();
            cmbUpperGraphic.Items.Add(Strings.General.none);
            cmbUpperGraphic.Items.AddRange(
                GameContentManager.GetSmartSortedTextureNames(GameContentManager.TextureType.Animation));

            mLowerWindow = new SwapChainRenderTarget(EditorGraphics.GetGraphicsDevice(), picLowerAnimation.Handle,
                picLowerAnimation.Width, picLowerAnimation.Height);
            mUpperWindow = new SwapChainRenderTarget(EditorGraphics.GetGraphicsDevice(), picUpperAnimation.Handle,
                picUpperAnimation.Width, picUpperAnimation.Height);
            mLowerDarkness = EditorGraphics.CreateRenderTexture(picLowerAnimation.Width, picLowerAnimation.Height);
            mUpperDarkness = EditorGraphics.CreateRenderTexture(picUpperAnimation.Width, picUpperAnimation.Height);

            InitLocalization();
            UpdateEditor();
        }

        private void InitLocalization()
        {
            Text = Strings.AnimationEditor.title;
            toolStripItemNew.Text = Strings.AnimationEditor.New;
            toolStripItemDelete.Text = Strings.AnimationEditor.delete;
            toolStripItemCopy.Text = Strings.AnimationEditor.copy;
            toolStripItemPaste.Text = Strings.AnimationEditor.paste;
            toolStripItemUndo.Text = Strings.AnimationEditor.undo;

            grpAnimations.Text = Strings.AnimationEditor.animations;

            grpGeneral.Text = Strings.AnimationEditor.general;
            lblName.Text = Strings.AnimationEditor.name;
            lblSound.Text = Strings.AnimationEditor.sound;
            labelDarkness.Text = Strings.AnimationEditor.simulatedarkness.ToString( scrlDarkness.Value);
            btnSwap.Text = Strings.AnimationEditor.swap;

            grpLower.Text = Strings.AnimationEditor.lowergroup;
            lblLowerGraphic.Text = Strings.AnimationEditor.lowergraphic;
            lblLowerHorizontalFrames.Text = Strings.AnimationEditor.lowerhorizontalframes;
            lblLowerVerticalFrames.Text = Strings.AnimationEditor.lowerverticalframes;
            lblLowerFrameCount.Text = Strings.AnimationEditor.lowerframecount;
            lblLowerFrameDuration.Text = Strings.AnimationEditor.lowerframeduration;
            lblLowerLoopCount.Text = Strings.AnimationEditor.lowerloopcount;
            grpLowerPlayback.Text = Strings.AnimationEditor.lowerplayback;
            lblLowerFrame.Text = Strings.AnimationEditor.lowerframe.ToString( scrlLowerFrame.Value);
            btnPlayLower.Text = Strings.AnimationEditor.lowerplay;
            grpLowerFrameOpts.Text = Strings.AnimationEditor.lowerframeoptions;
            btnLowerClone.Text = Strings.AnimationEditor.lowerclone;

            grpUpper.Text = Strings.AnimationEditor.uppergroup;
            lblUpperGraphic.Text = Strings.AnimationEditor.uppergraphic;
            lblUpperHorizontalFrames.Text = Strings.AnimationEditor.upperhorizontalframes;
            lblUpperVerticalFrames.Text = Strings.AnimationEditor.upperverticalframes;
            lblUpperFrameCount.Text = Strings.AnimationEditor.upperframecount;
            lblUpperFrameDuration.Text = Strings.AnimationEditor.upperframeduration;
            lblUpperLoopCount.Text = Strings.AnimationEditor.upperloopcount;
            grpUpperPlayback.Text = Strings.AnimationEditor.upperplayback;
            lblUpperFrame.Text = Strings.AnimationEditor.upperframe.ToString( scrlUpperFrame.Value);
            btnPlayUpper.Text = Strings.AnimationEditor.upperplay;
            grpUpperFrameOpts.Text = Strings.AnimationEditor.upperframeoptions;
            btnUpperClone.Text = Strings.AnimationEditor.upperclone;

            btnSave.Text = Strings.AnimationEditor.save;
            btnCancel.Text = Strings.AnimationEditor.cancel;
        }

        public void InitEditor()
        {
            lstAnimations.Items.Clear();
            lstAnimations.Items.AddRange(Database.GetGameObjectList(GameObjectType.Animation));
        }

        private void UpdateEditor()
        {
            if (mEditorItem != null)
            {
                pnlContainer.Show();

                txtName.Text = mEditorItem.Name;
                cmbSound.SelectedIndex = cmbSound.FindString(TextUtils.NullToNone(mEditorItem.Sound));

                cmbLowerGraphic.SelectedIndex =
                    cmbLowerGraphic.FindString(TextUtils.NullToNone(mEditorItem.Lower.Sprite));

                nudLowerHorizontalFrames.Value = mEditorItem.Lower.XFrames;
                nudLowerVerticalFrames.Value = mEditorItem.Lower.YFrames;
                nudLowerFrameCount.Value = mEditorItem.Lower.FrameCount;
                UpdateLowerFrames();

                nudLowerFrameDuration.Value = mEditorItem.Lower.FrameSpeed;
                tmrLowerAnimation.Interval = (int) nudLowerFrameDuration.Value;
                nudLowerLoopCount.Value = mEditorItem.Lower.LoopCount;

                cmbUpperGraphic.SelectedIndex =
                    cmbUpperGraphic.FindString(TextUtils.NullToNone(mEditorItem.Upper.Sprite));

                nudUpperHorizontalFrames.Value = mEditorItem.Upper.XFrames;
                nudUpperVerticalFrames.Value = mEditorItem.Upper.YFrames;
                nudUpperFrameCount.Value = mEditorItem.Upper.FrameCount;
                UpdateUpperFrames();

                nudUpperFrameDuration.Value = mEditorItem.Upper.FrameSpeed;
                tmrUpperAnimation.Interval = (int) nudUpperFrameDuration.Value;
                nudUpperLoopCount.Value = mEditorItem.Upper.LoopCount;

                chkDisableLowerRotations.Checked = mEditorItem.Lower.DisableRotations;
                chkDisableUpperRotations.Checked = mEditorItem.Upper.DisableRotations;

                LoadLowerLight();
                DrawLowerFrame();
                LoadUpperLight();
                DrawUpperFrame();

                if (mChanged.IndexOf(mEditorItem) == -1)
                {
                    mChanged.Add(mEditorItem);
                    mEditorItem.MakeBackup();
                }
            }
            else
            {
                pnlContainer.Hide();
            }
            UpdateToolStripItems();
        }

        private void txtName_TextChanged(object sender, EventArgs e)
        {
            mChangingName = true;
            mEditorItem.Name = txtName.Text;
            lstAnimations.Items[Database.GameObjectListIndex(GameObjectType.Animation, mEditorItem.Index)] =
                txtName.Text;
            mChangingName = false;
        }

        private void cmbSound_SelectedIndexChanged(object sender, EventArgs e)
        {
            mEditorItem.Sound = TextUtils.SanitizeNone(cmbSound?.Text);
        }

        private void cmbLowerGraphic_SelectedIndexChanged(object sender, EventArgs e)
        {
            mEditorItem.Lower.Sprite = TextUtils.SanitizeNone(cmbLowerGraphic?.Text);
        }

        private void cmbUpperGraphic_SelectedIndexChanged(object sender, EventArgs e)
        {
            mEditorItem.Upper.Sprite = TextUtils.SanitizeNone(cmbUpperGraphic?.Text);
        }

        private void tmrLowerAnimation_Tick(object sender, EventArgs e)
        {
            if (mPlayLower)
            {
                mLowerFrame++;
                if (mLowerFrame >= (int) nudLowerFrameCount.Value)
                {
                    mLowerFrame = 0;
                }
            }
        }

        void UpdateLowerFrames()
        {
            LightBase[] newArray;
            scrlLowerFrame.Maximum = (int) nudLowerFrameCount.Value;
            if (mEditorItem.Lower.FrameCount !=
                mEditorItem.Lower.Lights.Length)
            {
                newArray = new LightBase[mEditorItem.Lower.FrameCount];
                for (int i = 0; i < newArray.Length; i++)
                {
                    if (i < mEditorItem.Lower.Lights.Length)
                    {
                        newArray[i] = mEditorItem.Lower.Lights[i];
                    }
                    else
                    {
                        newArray[i] = new LightBase(-1, -1);
                    }
                }
                mEditorItem.Lower.Lights = newArray;
            }
        }

        void UpdateUpperFrames()
        {
            LightBase[] newArray;
            scrlUpperFrame.Maximum = (int) nudUpperFrameCount.Value;
            if (mEditorItem.Upper.FrameCount !=
                mEditorItem.Upper.Lights.Length)
            {
                newArray = new LightBase[mEditorItem.Upper.FrameCount];
                for (int i = 0; i < newArray.Length; i++)
                {
                    if (i < mEditorItem.Upper.Lights.Length)
                    {
                        newArray[i] = mEditorItem.Upper.Lights[i];
                    }
                    else
                    {
                        newArray[i] = new LightBase(-1, -1);
                    }
                }
                mEditorItem.Upper.Lights = newArray;
            }
        }

        void DrawLowerFrame()
        {
            if (mLowerWindow == null || mEditorItem == null) return;
            if (!mPlayLower) mLowerFrame = scrlLowerFrame.Value - 1;
            GraphicsDevice graphicsDevice = EditorGraphics.GetGraphicsDevice();
            EditorGraphics.EndSpriteBatch();
            graphicsDevice.SetRenderTarget(mLowerDarkness);
            graphicsDevice.Clear(Microsoft.Xna.Framework.Color.Black);
            if (mLowerFrame < mEditorItem.Lower.Lights.Length)
            {
                EditorGraphics.DrawLight(
                    picLowerAnimation.Width / 2 +
                    mEditorItem.Lower.Lights[mLowerFrame].OffsetX,
                    picLowerAnimation.Height / 2 +
                    mEditorItem.Lower.Lights[mLowerFrame].OffsetY,
                    mEditorItem.Lower.Lights[mLowerFrame], mLowerDarkness);
            }
            EditorGraphics.DrawTexture(EditorGraphics.GetWhiteTex(), new RectangleF(0, 0, 1, 1),
                new RectangleF(0, 0, mLowerDarkness.Width, mLowerDarkness.Height),
                System.Drawing.Color.FromArgb((byte) (((float) (100 - scrlDarkness.Value) / 100f) * 255), 255, 255,
                    255), mLowerDarkness,
                BlendState.Additive);
            EditorGraphics.EndSpriteBatch();
            graphicsDevice.SetRenderTarget(mLowerWindow);
            graphicsDevice.Clear(Microsoft.Xna.Framework.Color.White);
            Texture2D animTexture = GameContentManager.GetTexture(GameContentManager.TextureType.Animation,
                cmbLowerGraphic.Text);
            if (animTexture != null)
            {
                long w = animTexture.Width / (int) nudLowerHorizontalFrames.Value;
                long h = animTexture.Height / (int) nudLowerVerticalFrames.Value;
                long x = 0;
                if (mLowerFrame > 0)
                {
                    x = (mLowerFrame % (int) nudLowerHorizontalFrames.Value) * w;
                }
                long y = (int) Math.Floor(mLowerFrame / nudLowerHorizontalFrames.Value) * h;
                EditorGraphics.DrawTexture(animTexture, new RectangleF(x, y, w, h),
                    new RectangleF(picLowerAnimation.Width / 2 - (int) w / 2,
                        (int) picLowerAnimation.Height / 2 - (int) h / 2, w, h), mLowerWindow);
            }
            EditorGraphics.DrawTexture(mLowerDarkness, 0, 0, mLowerWindow, EditorGraphics.MultiplyState);
            EditorGraphics.EndSpriteBatch();
            mLowerWindow.Present();
        }

        void DrawUpperFrame()
        {
            if (mUpperWindow == null || mEditorItem == null) return;
            if (!mPlayUpper) mUpperFrame = scrlUpperFrame.Value - 1;
            GraphicsDevice graphicsDevice = EditorGraphics.GetGraphicsDevice();
            EditorGraphics.EndSpriteBatch();
            graphicsDevice.SetRenderTarget(mUpperDarkness);
            graphicsDevice.Clear(Microsoft.Xna.Framework.Color.Black);
            if (mUpperFrame < mEditorItem.Upper.Lights.Length)
            {
                EditorGraphics.DrawLight(
                    picUpperAnimation.Width / 2 +
                    mEditorItem.Upper.Lights[mUpperFrame].OffsetX,
                    picUpperAnimation.Height / 2 +
                    mEditorItem.Upper.Lights[mUpperFrame].OffsetY,
                    mEditorItem.Upper.Lights[mUpperFrame], mUpperDarkness);
            }
            EditorGraphics.DrawTexture(EditorGraphics.GetWhiteTex(), new RectangleF(0, 0, 1, 1),
                new RectangleF(0, 0, mUpperDarkness.Width, mUpperDarkness.Height),
                System.Drawing.Color.FromArgb((byte) (((float) (100 - scrlDarkness.Value) / 100f) * 255), 255, 255,
                    255), mUpperDarkness,
                BlendState.Additive);
            EditorGraphics.EndSpriteBatch();
            graphicsDevice.SetRenderTarget(mUpperWindow);
            graphicsDevice.Clear(Microsoft.Xna.Framework.Color.White);
            Texture2D animTexture = GameContentManager.GetTexture(GameContentManager.TextureType.Animation,
                cmbUpperGraphic.Text);
            if (animTexture != null)
            {
                long w = animTexture.Width / (int) nudUpperHorizontalFrames.Value;
                long h = animTexture.Height / (int) nudUpperVerticalFrames.Value;
                long x = 0;
                if (mUpperFrame > 0)
                {
                    x = (mUpperFrame % (int) nudUpperHorizontalFrames.Value) * w;
                }
                long y = (int) Math.Floor(mUpperFrame / nudUpperHorizontalFrames.Value) * h;
                EditorGraphics.DrawTexture(animTexture, new RectangleF(x, y, w, h),
                    new RectangleF(picUpperAnimation.Width / 2 - (int) w / 2,
                        (int) picUpperAnimation.Height / 2 - (int) h / 2, w, h), mUpperWindow);
            }
            EditorGraphics.DrawTexture(mUpperDarkness, 0, 0, mUpperWindow, EditorGraphics.MultiplyState);
            EditorGraphics.EndSpriteBatch();
            mUpperWindow.Present();
        }

        private void tmrUpperAnimation_Tick(object sender, EventArgs e)
        {
            if (mPlayUpper)
            {
                mUpperFrame++;
                if (mUpperFrame >= (int) nudUpperFrameCount.Value)
                {
                    mUpperFrame = 0;
                }
            }
        }

        private void frmAnimation_FormClosed(object sender, FormClosedEventArgs e)
        {
            Globals.CurrentEditor = -1;
        }

        private void scrlLowerFrame_Scroll(object sender, ScrollValueEventArgs e)
        {
            lblLowerFrame.Text = Strings.AnimationEditor.lowerframe.ToString( scrlLowerFrame.Value);
            LoadLowerLight();
            DrawLowerFrame();
        }

        private void LoadLowerLight()
        {
            lightEditorLower.CanClose = false;
            lightEditorLower.LoadEditor(mEditorItem.Lower.Lights[scrlLowerFrame.Value - 1]);
        }

        private void LoadUpperLight()
        {
            lightEditorUpper.CanClose = false;
            lightEditorUpper.LoadEditor(mEditorItem.Upper.Lights[scrlUpperFrame.Value - 1]);
        }

        private void scrlUpperFrame_Scroll(object sender, ScrollValueEventArgs e)
        {
            lblUpperFrame.Text = Strings.AnimationEditor.upperframe.ToString( scrlUpperFrame.Value);
            LoadUpperLight();
            DrawUpperFrame();
        }

        private void btnPlayLower_Click(object sender, EventArgs e)
        {
            mPlayLower = !mPlayLower;
            if (mPlayLower)
            {
                btnPlayLower.Text = Strings.AnimationEditor.lowerstop;
            }
            else
            {
                btnPlayLower.Text = Strings.AnimationEditor.lowerplay;
            }
        }

        private void btnLowerClone_Click(object sender, EventArgs e)
        {
            if (scrlLowerFrame.Value > 1)
            {
                mEditorItem.Lower.Lights[scrlLowerFrame.Value - 1] =
                    new LightBase(mEditorItem.Lower.Lights[scrlLowerFrame.Value - 2]);
                LoadLowerLight();
                DrawLowerFrame();
            }
        }

        private void btnPlayUpper_Click(object sender, EventArgs e)
        {
            mPlayUpper = !mPlayUpper;
            if (mPlayUpper)
            {
                btnPlayUpper.Text = Strings.AnimationEditor.upperstop;
            }
            else
            {
                btnPlayUpper.Text = Strings.AnimationEditor.upperplay;
            }
        }

        private void btnUpperClone_Click(object sender, EventArgs e)
        {
            if (scrlUpperFrame.Value > 1)
            {
                mEditorItem.Upper.Lights[scrlUpperFrame.Value - 1] =
                    new LightBase(mEditorItem.Upper.Lights[scrlUpperFrame.Value - 2]);
                LoadUpperLight();
                DrawUpperFrame();
            }
        }

        private void scrlDarkness_Scroll(object sender, ScrollValueEventArgs e)
        {
            labelDarkness.Text = Strings.AnimationEditor.simulatedarkness.ToString( scrlDarkness.Value);
        }

        private void btnSwap_Click(object sender, EventArgs e)
        {
            string lowerAnimSprite = mEditorItem.Lower.Sprite;
            int lowerAnimXFrames = mEditorItem.Lower.XFrames;
            int lowerAnimYFrames = mEditorItem.Lower.YFrames;
            int lowerAnimFrameCount = mEditorItem.Lower.FrameCount;
            int lowerAnimFrameSpeed = mEditorItem.Lower.FrameSpeed;
            int lowerAnimLoopCount = mEditorItem.Lower.LoopCount;
            bool disableLowerRotations = mEditorItem.Lower.DisableRotations;
            LightBase[] lowerLights = mEditorItem.Lower.Lights;
            mEditorItem.Lower.Sprite = mEditorItem.Upper.Sprite;
            mEditorItem.Lower.XFrames = mEditorItem.Upper.XFrames;
            mEditorItem.Lower.YFrames = mEditorItem.Upper.YFrames;
            mEditorItem.Lower.FrameCount = mEditorItem.Upper.FrameCount;
            mEditorItem.Lower.FrameSpeed = mEditorItem.Upper.FrameSpeed;
            mEditorItem.Lower.LoopCount = mEditorItem.Upper.LoopCount;
            mEditorItem.Lower.Lights = mEditorItem.Upper.Lights;
            mEditorItem.Lower.DisableRotations = mEditorItem.Upper.DisableRotations;


            mEditorItem.Upper.Sprite = lowerAnimSprite;
            mEditorItem.Upper.XFrames = lowerAnimXFrames;
            mEditorItem.Upper.YFrames = lowerAnimYFrames;
            mEditorItem.Upper.FrameCount = lowerAnimFrameCount;
            mEditorItem.Upper.FrameSpeed = lowerAnimFrameSpeed;
            mEditorItem.Upper.LoopCount = lowerAnimLoopCount;
            mEditorItem.Upper.Lights = lowerLights;
            mEditorItem.Upper.DisableRotations = disableLowerRotations;

            mUpperFrame = 0;
            mLowerFrame = 0;

            UpdateEditor();
        }

        private void toolStripItemNew_Click(object sender, EventArgs e)
        {
            PacketSender.SendCreateObject(GameObjectType.Animation);
        }

        private void toolStripItemDelete_Click(object sender, EventArgs e)
        {
            if (mEditorItem != null && lstAnimations.Focused)
            {
                if (DarkMessageBox.ShowWarning(Strings.AnimationEditor.deleteprompt,
                        Strings.AnimationEditor.deletetitle, DarkDialogButton.YesNo,
                        Properties.Resources.Icon) ==
                    DialogResult.Yes)
                {
                    PacketSender.SendDeleteObject(mEditorItem);
                }
            }
        }

        private void toolStripItemCopy_Click(object sender, EventArgs e)
        {
            if (mEditorItem != null && lstAnimations.Focused)
            {
                mCopiedItem = mEditorItem.JsonData;
                toolStripItemPaste.Enabled = true;
            }
        }

        private void toolStripItemPaste_Click(object sender, EventArgs e)
        {
            if (mEditorItem != null && mCopiedItem != null && lstAnimations.Focused)
            {
                mEditorItem.Load(mCopiedItem);
                UpdateEditor();
            }
        }

        private void toolStripItemUndo_Click(object sender, EventArgs e)
        {
            if (mChanged.Contains(mEditorItem) && mEditorItem != null)
            {
                if (DarkMessageBox.ShowWarning(Strings.AnimationEditor.undoprompt,
                        Strings.AnimationEditor.undotitle, DarkDialogButton.YesNo,
                        Properties.Resources.Icon) ==
                    DialogResult.Yes)
                {
                    mEditorItem.RestoreBackup();
                    UpdateEditor();
                }
            }
        }

        private void itemList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control)
            {
                if (e.KeyCode == Keys.Z)
                {
                    toolStripItemUndo_Click(null, null);
                }
                else if (e.KeyCode == Keys.V)
                {
                    toolStripItemPaste_Click(null, null);
                }
                else if (e.KeyCode == Keys.C)
                {
                    toolStripItemCopy_Click(null, null);
                }
            }
            else
            {
                if (e.KeyCode == Keys.Delete)
                {
                    toolStripItemDelete_Click(null, null);
                }
            }
        }

        private void UpdateToolStripItems()
        {
            toolStripItemCopy.Enabled = mEditorItem != null && lstAnimations.Focused;
            toolStripItemPaste.Enabled = mEditorItem != null && mCopiedItem != null && lstAnimations.Focused;
            toolStripItemDelete.Enabled = mEditorItem != null && lstAnimations.Focused;
            toolStripItemUndo.Enabled = mEditorItem != null && lstAnimations.Focused;
        }

        private void itemList_FocusChanged(object sender, EventArgs e)
        {
            UpdateToolStripItems();
        }

        private void form_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control)
            {
                if (e.KeyCode == Keys.N)
                {
                    toolStripItemNew_Click(null, null);
                }
            }
        }

        private void nudLowerHorizontalFrames_ValueChanged(object sender, EventArgs e)
        {
            mEditorItem.Lower.XFrames = (int) nudLowerHorizontalFrames.Value;
        }

        private void nudLowerVerticalFrames_ValueChanged(object sender, EventArgs e)
        {
            mEditorItem.Lower.YFrames = (int) nudLowerVerticalFrames.Value;
        }

        private void nudLowerFrameCount_ValueChanged(object sender, EventArgs e)
        {
            mEditorItem.Lower.FrameCount = (int) nudLowerFrameCount.Value;
            UpdateLowerFrames();
        }

        private void nudLowerFrameDuration_ValueChanged(object sender, EventArgs e)
        {
            mEditorItem.Lower.FrameSpeed = (int) nudLowerFrameDuration.Value;
            tmrLowerAnimation.Interval = (int) nudLowerFrameDuration.Value;
        }

        private void nudLowerLoopCount_ValueChanged(object sender, EventArgs e)
        {
            mEditorItem.Lower.LoopCount = (int) nudLowerLoopCount.Value;
        }

        private void nudUpperHorizontalFrames_ValueChanged(object sender, EventArgs e)
        {
            mEditorItem.Upper.XFrames = (int) nudUpperHorizontalFrames.Value;
        }

        private void nudUpperVerticalFrames_ValueChanged(object sender, EventArgs e)
        {
            mEditorItem.Upper.YFrames = (int) nudUpperVerticalFrames.Value;
        }

        private void nudUpperFrameCount_ValueChanged(object sender, EventArgs e)
        {
            mEditorItem.Upper.FrameCount = (int) nudUpperFrameCount.Value;
            UpdateUpperFrames();
        }

        private void nudUpperFrameDuration_ValueChanged(object sender, EventArgs e)
        {
            mEditorItem.Upper.FrameSpeed = (int) nudUpperFrameDuration.Value;
            tmrUpperAnimation.Interval = (int) nudUpperFrameDuration.Value;
        }

        private void nudUpperLoopCount_ValueChanged(object sender, EventArgs e)
        {
            mEditorItem.Upper.LoopCount = (int) nudUpperLoopCount.Value;
        }

        private void tmrRender_Tick(object sender, EventArgs e)
        {
            DrawLowerFrame();
            DrawUpperFrame();
        }

        private void chkDisableLowerRotations_CheckedChanged(object sender, EventArgs e)
        {
            mEditorItem.Lower.DisableRotations = chkDisableLowerRotations.Checked;
        }

        private void chkDisableUpperRotations_CheckedChanged(object sender, EventArgs e)
        {
            mEditorItem.Upper.DisableRotations = chkDisableUpperRotations.Checked;
        }
    }
}
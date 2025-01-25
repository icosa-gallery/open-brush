﻿// Copyright 2020 The Tilt Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using UnityEngine;
using UnityEngine.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine.Serialization;

namespace TiltBrush
{
    public class SketchbookPanel : ModalPanel
    {
        public enum RootSet
        {
            Local,
            Remote,
            Liked,
            Backup,
        }

        public static SketchbookPanel Instance => PanelManager.m_Instance.GetSketchBookPanel() as SketchbookPanel;

        // Index of the "local sketches" button in m_GalleryButtons
        const int kElementNumberGalleryButtonLocal = 0;
        // Amount of extra space to put below the "local sketches" gallery button
        const float kGalleryButtonLocalPadding = .15f;

        [SerializeField] private Texture2D m_LoadingImageTexture;
        [SerializeField] private Texture2D m_UnknownImageTexture;
        [SerializeField] private TextMeshPro m_PanelTextPro;
        [SerializeField] private LocalizedString m_PanelTextStandard;
        public string PanelTextStandard { get { return m_PanelTextStandard.GetLocalizedStringAsync().Result; } }
        [SerializeField] private LocalizedString m_PanelTextShowcase;
        public string PanelTextShowcase { get { return m_PanelTextShowcase.GetLocalizedStringAsync().Result; } }

        [SerializeField] private LocalizedString m_PanelTextLiked;
        public string PanelTextLiked { get { return m_PanelTextLiked.GetLocalizedStringAsync().Result; } }
        [SerializeField] private LocalizedString m_PanelTextDrive;
        public string PanelTextDrive { get { return m_PanelTextDrive.GetLocalizedStringAsync().Result; } }
        [SerializeField] private GameObject m_NoSketchesMessage;
        [SerializeField] private GameObject m_NoDriveSketchesMessage;
        [SerializeField] private GameObject m_NoLikesMessage;
        [SerializeField] private GameObject m_NotLoggedInMessage;
        [SerializeField] private GameObject m_NotLoggedInDriveMessage;
        [SerializeField] private GameObject m_NoShowcaseMessage;
        [SerializeField] private GameObject m_ContactingServerMessage;
        [SerializeField] private GameObject m_OutOfDateMessage;
        [SerializeField] private GameObject m_NoPolyConnectionMessage;
        [SerializeField] private GameObject[] m_IconsOnFirstPage;
        [SerializeField] private GameObject[] m_IconsOnNormalPage;
        [SerializeField] private GameObject m_CloseButton;
        [SerializeField] private GameObject m_NewSketchButton;
        // Gallery buttons will automatically reposition based on how many are visible so they must be
        // added to this array in order from top to bottom.
        [SerializeField] private ActionButton m_BackButton;
        [SerializeField] private GalleryButton[] m_GalleryButtons;
        [SerializeField] private int m_ElementNumberGalleryButtonDrive = 3;
        [SerializeField] private float m_GalleryButtonHeight = 0.3186f;
        [SerializeField] private Renderer m_ProfileButtonRenderer;
        [SerializeField] private GameObject m_LoadingGallery;
        [SerializeField] private GameObject m_DriveSyncProgress;
        [SerializeField] private GameObject m_SyncingDriveIcon;
        [SerializeField] private GameObject m_DriveEnabledIcon;
        [SerializeField] private GameObject m_DriveDisabledIcon;
        [SerializeField] private GameObject m_DriveFullIcon;
        [SerializeField] private Vector2 m_SketchIconUvScale = new Vector2(0.7f, 0.7f);
        [SerializeField] private Vector3 m_ReadOnlyPopupOffset;

        [FormerlySerializedAs("m_FolderTexture")][SerializeField] private Texture2D m_FolderIcon;

        private float m_ImageAspect;
        private Vector2 m_HalfInvUvScale;

        private SceneFileInfo m_FirstSketch;

        private bool m_AllIconTexturesAssigned;
        private bool m_AllSketchesAreAvailable;
        private Stack<ISketchSet>[] m_SetStacks;
        private int m_SelectedStack;


        private OptionButton m_NewSketchButtonScript;
        private OptionButton m_PaintButtonScript;
        private List<BaseButton> m_IconScriptsOnFirstPage;
        private List<BaseButton> m_IconScriptsOnNormalPage;
        private bool m_DriveSetHasSketches;
        private bool m_ReadOnlyShown = false;

        public float ImageAspect { get { return m_ImageAspect; } }

        public int SelectedSketchStack => m_SelectedStack;
        public ISketchSet CurrentSketchSet { get; private set; }

        override public void SetInIntroMode(bool inIntro)
        {
            m_NewSketchButton.SetActive(inIntro);
            m_CloseButton.SetActive(!inIntro);

            // When we switch in to intro mode, make our panel colorful, even if it doesn't have focus,
            // to help attract attention.
            if (inIntro)
            {
                for (int i = 0; i < m_IconScriptsOnFirstPage.Count; ++i)
                {
                    m_IconScriptsOnFirstPage[i].SetButtonGrayscale(false);
                }
                for (int i = 0; i < m_IconScriptsOnNormalPage.Count; ++i)
                {
                    m_IconScriptsOnNormalPage[i].SetButtonGrayscale(false);
                }
            }
        }

        protected override List<BaseButton> Icons
        {
            get
            {
                return (PageIndex == 0 ? m_IconScriptsOnFirstPage : m_IconScriptsOnNormalPage);
            }
        }

        public override bool IsInButtonMode(ModeButton button)
        {
            GalleryButton galleryButton = button as GalleryButton;
            // TODO: There's gotta be a better way of doing this!
            return galleryButton &&
                ((galleryButton.m_ButtonType == GalleryButton.Type.Liked && m_SelectedStack == 2) ||
                (galleryButton.m_ButtonType == GalleryButton.Type.Local && m_SelectedStack == 0) ||
                (galleryButton.m_ButtonType == GalleryButton.Type.Showcase && m_SelectedStack == 1) ||
                (galleryButton.m_ButtonType == GalleryButton.Type.Drive && m_SelectedStack == 3));
        }

        override public void InitPanel()
        {
            base.InitPanel();

            m_NewSketchButtonScript = m_NewSketchButton.GetComponent<OptionButton>();
            m_PaintButtonScript = m_CloseButton.GetComponent<OptionButton>();
            m_IconScriptsOnFirstPage = new List<BaseButton>();
            for (int i = 0; i < m_IconsOnFirstPage.Length; ++i)
            {
                m_IconScriptsOnFirstPage.Add(m_IconsOnFirstPage[i].GetComponent<BaseButton>());
            }
            m_IconScriptsOnNormalPage = new List<BaseButton>();
            for (int i = 0; i < m_IconsOnNormalPage.Length; ++i)
            {
                m_IconScriptsOnNormalPage.Add(m_IconsOnNormalPage[i].GetComponent<BaseButton>());
            }
            SetInIntroMode(false);

            Debug.Assert(m_SketchIconUvScale.x >= 0.0f && m_SketchIconUvScale.x <= 1.0f &&
                m_SketchIconUvScale.y >= 0.0f && m_SketchIconUvScale.y <= 1.0f);
            m_HalfInvUvScale.Set(1.0f - m_SketchIconUvScale.x, 1.0f - m_SketchIconUvScale.y);
            m_HalfInvUvScale *= 0.5f;
        }

        private void InitializeRootSketchSets()
        {
            m_SetStacks = new Stack<ISketchSet>[4];

        }

        public ISketchSet GetSketchSet(RootSet set)
        {
            return m_SetStacks[(int)set].Peek();
        }

        protected override void OnStart()
        {
            // Initialize icons.
            LoadSketchButton[] rPanelButtons = m_Mesh.GetComponentsInChildren<LoadSketchButton>();
            foreach (LoadSketchButton icon in rPanelButtons)
            {
                GameObject go = icon.gameObject;
                go.SetActive(false);
            }

            // GameObject is active in prefab so the button registers.
            m_NoLikesMessage.SetActive(false);
            m_NotLoggedInMessage.SetActive(false);
            m_NotLoggedInDriveMessage.SetActive(false);

            var rssOptions = new Dictionary<string, object>
            {
                {"uri",  "https://timaidley.github.io/open-brush-feed/sketches.rss" }
            };

            var fileOptions = new Dictionary<string, object>
            {
                {"path", App.UserSketchPath() },
                {"name", "Your Sketches"},
                {"icon", m_FolderIcon},
            };

            m_SetStacks = new string[]
            {
                $"file:///{App.UserSketchPath()}",
                "feed:https://timaidley.github.io/open-brush-feed/sketches.rss",
                IcosaCollection.AllAssetsUri.OriginalString,
                "googledrive:"
            }.Select(uri => new Stack<ISketchSet>(new[] { SketchCatalog.m_Instance.GetSketchSet(uri) })).ToArray();

            m_SelectedStack = (int)RootSet.Backup;
            CurrentSketchSet = m_SetStacks[m_SelectedStack].Peek();

            // Dynamically position the gallery buttons.
            OnDriveSetHasSketchesChanged();

            // Set the sketch set var to Liked, then function set to force state.
            SetVisibleSketchSet(0);
            RefreshPage();

            App.GoogleIdentity.OnLogout += OnSketchRefreshingChanged;
        }

        void OnSketchRefreshingChanged()
        {
            if (m_ContactingServerMessage.activeSelf ||
                m_NoShowcaseMessage.activeSelf ||
                m_LoadingGallery.activeSelf)
            {
                // Update the overlays more frequently when these overlays are shown to reflect whether
                // we are actively trying to get sketches from Poly.
                RefreshPage();
            }
        }

        void OnDestroy()
        {
            if (CurrentSketchSet != null)
            {
                CurrentSketchSet.OnChanged -= OnSketchSetDirty;
                CurrentSketchSet.OnSketchRefreshingChanged -= OnSketchRefreshingChanged;
            }
        }

        override protected void OnEnablePanel()
        {
            base.OnEnablePanel();
            if (CurrentSketchSet != null)
            {
                CurrentSketchSet.RequestRefresh();
            }
        }

        public void PushSketchSet(int stack, ISketchSet sketchSet)
        {
            m_SetStacks[stack].Push(sketchSet);
            if (stack == m_SelectedStack)
            {
                SetVisibleSketchSet((RootSet)m_SelectedStack);
            }
        }

        public void PopSketchSet(int stack)
        {
            m_SetStacks[stack].Pop();
            if (stack == m_SelectedStack)
            {
                SetVisibleSketchSet((RootSet)m_SelectedStack);
            }
        }

        void SetVisibleSketchSet(RootSet stack)
        {
            int stackIndex = (int)stack;
            var newSketchSet = m_SetStacks[stackIndex].Peek();
            if (newSketchSet != CurrentSketchSet)
            {
                // Clean up our old sketch set.
                if (CurrentSketchSet != null)
                {
                    CurrentSketchSet.OnChanged -= OnSketchSetDirty;
                    CurrentSketchSet.OnSketchRefreshingChanged -= OnSketchRefreshingChanged;
                }

                // Cache new set.
                m_SelectedStack = stackIndex;
                CurrentSketchSet = m_SetStacks[m_SelectedStack].Peek();
                CurrentSketchSet.OnChanged += OnSketchSetDirty;
                CurrentSketchSet.OnSketchRefreshingChanged += OnSketchRefreshingChanged;
                CurrentSketchSet.RequestRefresh();

                // Tell all the icons which set to reference when loading sketches.
                IEnumerable<LoadSketchButton> allIcons = m_IconsOnFirstPage.Concat(m_IconsOnNormalPage)
                    .Select(icon => icon.GetComponent<LoadSketchButton>())
                    .Where(icon => icon != null);
                foreach (LoadSketchButton icon in allIcons)
                {
                    icon.SketchSet = CurrentSketchSet;
                }

                ComputeNumPages();
                ResetPageIndex();
                RefreshPage();

                m_PanelTextPro.text = CurrentSketchSet.Title;
            }
        }

        private void ComputeNumPages()
        {
            if (CurrentSketchSet.NumSketches <= m_IconsOnFirstPage.Length)
            {
                m_NumPages = 1;
                return;
            }
            int remainingSketches = CurrentSketchSet.NumSketches - m_IconsOnFirstPage.Length;
            int normalPages = ((remainingSketches - 1) / m_IconsOnNormalPage.Length) + 1;
            m_NumPages = 1 + normalPages;
        }

        List<int> GetIconLoadIndices()
        {
            var ret = new List<int>();
            for (int i = 0; i < Icons.Count; i++)
            {
                int sketchIndex = m_IndexOffset + i;
                if (sketchIndex >= CurrentSketchSet.NumSketches)
                {
                    break;
                }
                ret.Add(sketchIndex);
            }
            return ret;
        }

        protected override void RefreshPage()
        {
            CurrentSketchSet.RequestOnlyLoadedMetadata(GetIconLoadIndices());
            m_AllIconTexturesAssigned = false;
            m_AllSketchesAreAvailable = false;

            // Disable all.
            foreach (var i in m_IconsOnFirstPage)
            {
                i.SetActive(false);
            }
            foreach (var i in m_IconsOnNormalPage)
            {
                i.SetActive(false);
            }

            // Base Refresh updates the modal parts of the panel, and we always want those refreshed.
            base.RefreshPage();

            m_NoSketchesMessage.SetActive(false);
            m_NoDriveSketchesMessage.SetActive(false);
            m_NotLoggedInMessage.SetActive(false);
            m_NoLikesMessage.SetActive(false);
            m_ContactingServerMessage.SetActive(false);
            m_NoShowcaseMessage.SetActive(false);

            // bool requiresPoly = CurrentSketchSet.SketchSetType == PolySketchSet.UriName;
            //
            // bool polyDown = VrAssetService.m_Instance.NoConnection && requiresPoly;
            // m_NoPolyConnectionMessage.SetActive(polyDown);
            //
            // bool outOfDate = !polyDown && !VrAssetService.m_Instance.Available && requiresPoly;
            // m_OutOfDateMessage.SetActive(outOfDate);
            //
            // if (outOfDate || polyDown)
            // {
            //     m_NoSketchesMessage.SetActive(false);
            //     m_NoDriveSketchesMessage.SetActive(false);
            //     m_NotLoggedInMessage.SetActive(false);
            //     m_NoLikesMessage.SetActive(false);
            //     m_ContactingServerMessage.SetActive(false);
            //     m_NoShowcaseMessage.SetActive(false);
            //     return;
            // }
            //
            // bool refreshIcons = CurrentSketchSet.NumSketches > 0;
            //
            // // Show no sketches if we don't have sketches.
            // bool isUser = CurrentSketchSet.SketchSetType == FileSketchSet.TypeName;
            // bool isLiked = CurrentSketchSet.SketchSetType == PolySketchSet.UriName;
            // bool isCurated = CurrentSketchSet is ResourceCollectionSketchSet;
            // bool isDrive = CurrentSketchSet.SketchSetType == GoogleDriveSketchSet.UriString;
            // m_NoSketchesMessage.SetActive(isUser && (CurrentSketchSet.NumSketches <= 0));
            // m_NoDriveSketchesMessage.SetActive(isDrive && (CurrentSketchSet.NumSketches <= 0));
            //
            // // Show sign in popup if signed out for liked or drive sketchsets
            // bool showNotLoggedIn = !App.GoogleIdentity.LoggedIn && (isLiked || isDrive);
            // refreshIcons = refreshIcons && !showNotLoggedIn;
            // m_NotLoggedInMessage.SetActive(showNotLoggedIn && isLiked);
            // m_NotLoggedInDriveMessage.SetActive(showNotLoggedIn && isDrive);
            //
            // // Show no likes text & gallery button if we don't have liked sketches.
            // m_NoLikesMessage.SetActive(
            //     isLiked &&
            //     (CurrentSketchSet.NumSketches <= 0) &&
            //     !CurrentSketchSet.IsActivelyRefreshingSketches &&
            //     App.GoogleIdentity.LoggedIn);
            //
            // // Show Contacting Server if we're talking to Poly.
            // m_ContactingServerMessage.SetActive(
            //     (requiresPoly || isDrive) &&
            //     (CurrentSketchSet.NumSketches <= 0) &&
            //     (CurrentSketchSet.IsActivelyRefreshingSketches && App.GoogleIdentity.LoggedIn));
            //
            // // Show Showcase error if we're in Showcase and don't have sketches.
            // m_NoShowcaseMessage.SetActive(
            //     isCurated &&
            //     (CurrentSketchSet.NumSketches <= 0) &&
            //     !CurrentSketchSet.IsActivelyRefreshingSketches);

            bool refreshIcons = CurrentSketchSet.NumSketches > 0;

            // Refresh all icons if necessary.
            if (!refreshIcons)
            {
                return;
            }

            for (int i = 0; i < Icons.Count; i++)
            {
                LoadSketchButton icon = Icons[i] as LoadSketchButton;
                // Default to loading image
                icon.SetButtonTexture(m_LoadingImageTexture);
                icon.ThumbnailLoaded = false;

                // Set sketch index relative to page based index
                int iSketchIndex = m_IndexOffset + i;
                if (iSketchIndex >= CurrentSketchSet.NumSketches)
                {
                    iSketchIndex = -1;
                }
                icon.SketchIndex = iSketchIndex;
                icon.ResetScale();

                // Init icon according to availability of sketch
                GameObject go = icon.gameObject;
                if (CurrentSketchSet.IsSketchIndexValid(iSketchIndex))
                {
                    string sSketchName = CurrentSketchSet.GetSketchName(iSketchIndex);
                    icon.SetDescriptionText(App.ShortenForDescriptionText(sSketchName));
                    SceneFileInfo info = CurrentSketchSet.GetSketchSceneFileInfo(iSketchIndex);
                    if (info.Available)
                    {
                        CurrentSketchSet.PrecacheSketchModels(iSketchIndex);
                    }

                    if (info.TriangleCount is int triCount)
                    {
                        icon.WarningVisible = triCount >
                            QualityControls.m_Instance.AppQualityLevels.WarningPolySketchTriangles;
                    }
                    else
                    {
                        icon.WarningVisible = false;
                    }
                    go.SetActive(true);
                }
                else
                {
                    go.SetActive(false);
                }
            }
        }

        void Update()
        {
            BaseUpdate();
            PageFlipUpdate();

            // Refresh icons while they are in flux
            if (CurrentSketchSet.IsReadyForAccess &&
                (!CurrentSketchSet.RequestedIconsAreLoaded ||
                !m_AllIconTexturesAssigned || !m_AllSketchesAreAvailable))
            {
                UpdateIcons();
            }

            // Set icon uv offsets relative to head position.
            Vector3 head_LS = m_Mesh.transform.InverseTransformPoint(ViewpointScript.Head.position);
            float angleX = Vector3.Angle(Vector3.back, new Vector3(head_LS.x, 0.0f, head_LS.z));
            angleX *= (head_LS.x > 0.0f) ? -1.0f : 1.0f;

            float angleY = Vector3.Angle(Vector3.back, new Vector3(0.0f, head_LS.y, head_LS.z));
            angleY *= (head_LS.y > 0.0f) ? -1.0f : 1.0f;

            float maxAngleXRatio = angleX / 90.0f;
            float maxAngleYRatio = angleY / 90.0f;
            Vector2 offset = new Vector2(
                m_HalfInvUvScale.x + (m_HalfInvUvScale.x * maxAngleXRatio),
                m_HalfInvUvScale.y + (m_HalfInvUvScale.y * maxAngleYRatio));
            for (int i = 0; i < Icons.Count; i++)
            {
                LoadSketchButton icon = Icons[i] as LoadSketchButton;
                icon.UpdateUvOffsetAndScale(offset, m_SketchIconUvScale);
            }

            switch (SelectedSketchStack)
            {
                case (int)RootSet.Liked:
                    m_LoadingGallery.SetActive(CurrentSketchSet.IsActivelyRefreshingSketches);
                    m_DriveSyncProgress.SetActive(false);
                    m_SyncingDriveIcon.SetActive(false);
                    m_DriveEnabledIcon.SetActive(false);
                    m_DriveDisabledIcon.SetActive(false);
                    m_DriveFullIcon.SetActive(false);
                    break;
                case (int)RootSet.Remote:
                    m_LoadingGallery.SetActive(false);
                    m_DriveSyncProgress.SetActive(false);
                    m_SyncingDriveIcon.SetActive(false);
                    m_DriveEnabledIcon.SetActive(false);
                    m_DriveDisabledIcon.SetActive(false);
                    m_DriveFullIcon.SetActive(false);
                    break;
                case (int)RootSet.Local:
                case (int)RootSet.Backup:
                    bool sketchSetRefreshing = CurrentSketchSet.SketchSetType == GoogleDriveSketchSet.UriString &&
                        CurrentSketchSet.IsActivelyRefreshingSketches;
                    bool driveSyncing = App.DriveSync.Syncing;
                    bool syncEnabled = App.DriveSync.SyncEnabled;
                    bool googleLoggedIn = App.GoogleIdentity.LoggedIn;
                    bool driveFull = App.DriveSync.DriveIsLowOnSpace;
                    m_LoadingGallery.SetActive(sketchSetRefreshing && !driveSyncing);
                    m_DriveSyncProgress.SetActive(driveSyncing && !driveFull);
                    m_SyncingDriveIcon.SetActive(driveSyncing && !driveFull);
                    m_DriveEnabledIcon.SetActive(!driveFull && !driveSyncing && syncEnabled && googleLoggedIn);
                    m_DriveDisabledIcon.SetActive(!syncEnabled && googleLoggedIn);
                    m_DriveFullIcon.SetActive(driveFull && syncEnabled && googleLoggedIn);
                    break;
            }

            // Check to see if whether "drive set has sketches" has changed.
            bool driveSetHasSketches = m_SetStacks[(int)RootSet.Backup].Peek().NumSketches != 0;
            if (m_DriveSetHasSketches != driveSetHasSketches)
            {
                m_DriveSetHasSketches = driveSetHasSketches;
                OnDriveSetHasSketchesChanged();
            }
        }

        // Whether or not the Google Drive set has any sketches impacts how the gallery buttons are
        // laid out.
        private void OnDriveSetHasSketchesChanged()
        {
            // Only show the Google Drive gallery tab if there are sketches in there.
            int galleryButtonAvailable = m_GalleryButtons.Length;
            int galleryButtonN;
            if (m_DriveSetHasSketches)
            {
                m_GalleryButtons[m_ElementNumberGalleryButtonDrive].gameObject.SetActive(true);
                galleryButtonN = galleryButtonAvailable;
            }
            else
            {
                m_GalleryButtons[m_ElementNumberGalleryButtonDrive].gameObject.SetActive(false);
                galleryButtonN = galleryButtonAvailable - 1;

                if (CurrentSketchSet.SketchSetType == GoogleDriveSketchSet.UriString)
                {
                    // We were on the Drive tab but it's gone away so switch to the local tab by simulating
                    // the user pressing the local tab button.
                    ButtonPressed(GalleryButton.Type.Local);
                }
            }

            // Position the gallery buttons so that they're centered.
            float buttonPosY = (0.5f * (galleryButtonN - 1) * m_GalleryButtonHeight
                + kGalleryButtonLocalPadding);
            for (int i = 0; i < galleryButtonAvailable; i++)
            {
                if (i == m_ElementNumberGalleryButtonDrive && !m_DriveSetHasSketches)
                {
                    continue;
                }
                Vector3 buttonPos = m_GalleryButtons[i].transform.localPosition;
                buttonPos.y = buttonPosY;
                m_GalleryButtons[i].transform.localPosition = buttonPos;
                buttonPosY -= m_GalleryButtonHeight;
                if (i == kElementNumberGalleryButtonLocal)
                {
                    buttonPosY -= kGalleryButtonLocalPadding;
                }
            }
        }

        // UpdateIcons() is called repeatedly by Update() until these three conditions are met:
        // 1: The SketchSet has loaded all the requested icons
        // 2: The textures for all the buttons have been set
        // 3: (Cloud only) The SketchSet has downloaded the corresponding .tilt files.
        //    Until the .tilt file is downloaded we set a fade on the button, and need to keep updating
        //    until the file is downloaded.
        private void UpdateIcons()
        {
            m_AllIconTexturesAssigned = true;
            m_AllSketchesAreAvailable = true;

            // Poll sketch catalog until icons have loaded
            foreach (BaseButton baseButton in Icons)
            {
                LoadSketchButton icon = baseButton as LoadSketchButton;
                if (icon == null) { continue; }
                int iSketchIndex = icon.SketchIndex;
                if (CurrentSketchSet.IsSketchIndexValid(iSketchIndex))
                {
                    icon.FadeIn = CurrentSketchSet.GetSketchSceneFileInfo(iSketchIndex).Available ? 1f : 0.5f;

                    if (!icon.ThumbnailLoaded)
                    {
                        Texture2D rTexture = null;
                        string[] authors;
                        string description;
                        if (CurrentSketchSet.GetSketchIcon(iSketchIndex, out rTexture, out authors, out description))
                        {
                            if (rTexture != null)
                            {
                                // Pass through aspect ratio of image so we don't get squished
                                // thumbnails from Poly
                                m_ImageAspect = (float)rTexture.width / rTexture.height;
                                float aspect = m_ImageAspect;
                                icon.SetButtonTexture(rTexture, aspect);
                            }
                            else
                            {
                                icon.SetButtonTexture(m_UnknownImageTexture);
                            }

                            // Mark the texture as assigned regardless of actual bits being valid
                            icon.ThumbnailLoaded = true;
                            ;
                            List<string> lines = new List<string>();
                            lines.Add(icon.Description);

                            SceneFileInfo info = CurrentSketchSet.GetSketchSceneFileInfo(iSketchIndex);
                            if (info is PolySceneFileInfo polyInfo &&
                                polyInfo.License != VrAssetService.kCreativeCommonsLicense)
                            {
                                lines.Add(String.Format("© {0}", authors[0]));
                                lines.Add("All Rights Reserved");
                            }
                            else
                            {
                                // Include primary author in description if available
                                if (authors != null && authors.Length > 0)
                                {
                                    lines.Add(authors[0]);
                                }
                                // Include an actual description
                                if (description != null)
                                {
                                    lines.Add(App.ShortenForDescriptionText(description));
                                }
                            }
                            icon.SetDescriptionText(lines.ToArray());
                        }
                        else
                        {
                            // While metadata has not finished loading, check if this file is valid
                            bool bFileValid = false;
                            SceneFileInfo rInfo = CurrentSketchSet.GetSketchSceneFileInfo(iSketchIndex);
                            if (rInfo != null)
                            {
                                bFileValid = rInfo.Exists;
                            }

                            // If this file isn't valid, just keep the defaults and move on
                            if (!bFileValid)
                            {
                                icon.SetButtonTexture(m_UnknownImageTexture);
                                icon.ThumbnailLoaded = true;
                            }
                            else
                            {
                                m_AllIconTexturesAssigned = false;
                            }
                            if (!rInfo.Available)
                            {
                                m_AllSketchesAreAvailable = false;
                            }
                        }
                    }
                }
            }
        }

        override public void OnUpdatePanel(Vector3 vToPanel, Vector3 vHitPoint)
        {
            base.OnUpdatePanel(vToPanel, vHitPoint);

            // Icons are active when animations aren't.
            bool bButtonsAvailable =
                (m_CurrentPageFlipState == PageFlipState.Standard) && (m_ActivePopUp == null);

            if (!PanelManager.m_Instance.IntroSketchbookMode)
            {
                if (bButtonsAvailable &&
                    DoesRayHitCollider(m_ReticleSelectionRay, m_PaintButtonScript.GetCollider()))
                {
                    m_PaintButtonScript.UpdateButtonState(m_InputValid);
                }
                else
                {
                    m_PaintButtonScript.ResetState();
                }
            }
            else
            {
                if (bButtonsAvailable &&
                    DoesRayHitCollider(m_ReticleSelectionRay, m_NewSketchButtonScript.GetCollider()))
                {
                    m_NewSketchButtonScript.UpdateButtonState(m_InputValid);
                }
                else
                {
                    m_NewSketchButtonScript.ResetState();
                }
            }
        }

        override protected void OnUpdateActive()
        {
            // If we're not active, hide all our preview panels
            if (!m_GazeActive)
            {
                m_ProfileButtonRenderer.material.SetFloat("_Grayscale", 1);

                for (int i = 0; i < m_IconScriptsOnFirstPage.Count; ++i)
                {
                    m_IconScriptsOnFirstPage[i].ResetState();
                }
                for (int i = 0; i < m_IconScriptsOnNormalPage.Count; ++i)
                {
                    m_IconScriptsOnNormalPage[i].ResetState();
                }
                if (m_NewSketchButtonScript)
                {
                    m_NewSketchButtonScript.ResetState();
                }
                if (m_PaintButtonScript)
                {
                    m_PaintButtonScript.ResetState();
                }
            }
            else if (m_CurrentState == PanelState.Available)
            {
                m_ProfileButtonRenderer.material.SetFloat("_Grayscale", 0);
                CurrentSketchSet.RequestRefresh();
            }
        }

        override protected void OnUpdateGazeBehavior(Color rPanelColor)
        {
            // Set the appropriate dim value for all our buttons and sliders
            if (Icons != null)
            {
                foreach (BaseButton icon in Icons)
                {
                    icon.SetColor(rPanelColor);
                }
            }

            if (m_NewSketchButtonScript != null)
            {
                m_NewSketchButtonScript.SetColor(rPanelColor);
            }

            if (m_NavigationButtons != null)
            {
                for (int i = 0; i < m_NavigationButtons.Length; ++i)
                {
                    m_NavigationButtons[i].SetColor(rPanelColor);
                }
            }
        }

        override public bool RaycastAgainstMeshCollider(Ray rRay, out RaycastHit rHitInfo, float fDist)
        {
            if (m_NewSketchButton.GetComponent<Collider>().Raycast(rRay, out rHitInfo, fDist))
            {
                return true;
            }
            return base.RaycastAgainstMeshCollider(rRay, out rHitInfo, fDist);
        }

        // Works specifically with GalleryButtons.
        public void ButtonPressed(GalleryButton.Type rType, BaseButton button = null)
        {
            // TODO: Just do this whole damn thing differently.
            switch (rType)
            {
                case GalleryButton.Type.Exit:
                    SketchSurfacePanel.m_Instance.EnableDefaultTool();
                    PointerManager.m_Instance.EatLineEnabledInput();
                    break;
                case GalleryButton.Type.Showcase:
                    SetVisibleSketchSet(RootSet.Remote);
                    break;
                case GalleryButton.Type.Local:
                    SetVisibleSketchSet(RootSet.Local);
                    break;
                case GalleryButton.Type.Liked:
                    SetVisibleSketchSet(RootSet.Liked);
                    break;
                case GalleryButton.Type.Drive:
                    SetVisibleSketchSet(RootSet.Backup);
                    if (!m_ReadOnlyShown)
                    {
                        CreatePopUp(SketchControlsScript.GlobalCommands.ReadOnlyNotice,
                            -1, -1, m_ReadOnlyPopupOffset);
                        if (button != null)
                        {
                            button.ResetState();
                        }
                        m_ReadOnlyShown = true;
                    }
                    break;
                default:
                    break;
            }
        }

        public void BackButtonPressed()
        {
            PopSketchSet(m_SelectedStack);
        }

        private void OnSketchSetDirty()
        {
            ComputeNumPages();

            SceneFileInfo first = (CurrentSketchSet.NumSketches > 0) ?
                CurrentSketchSet.GetSketchSceneFileInfo(0) : null;
            // If first sketch changed, return to first page.
            if (m_FirstSketch != null && !m_FirstSketch.Equals(first))
            {
                PageIndex = 0;
            }
            else
            {
                PageIndex = Mathf.Min(PageIndex, m_NumPages - 1);
            }
            m_FirstSketch = first;
            GotoPage(PageIndex);
            UpdateIndexOffset();
            RefreshPage();
        }

        override protected void UpdateIndexOffset()
        {
            m_IndexOffset = PageIndex == 0 ? 0 : m_IconsOnFirstPage.Length + (PageIndex - 1) * Icons.Count;
        }
    }
} // namespace TiltBrush

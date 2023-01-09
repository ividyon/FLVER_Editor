﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Xml.Serialization;
using Assimp;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using SoulsFormats;
using Color = System.Drawing.Color;
using Matrix4x4 = System.Numerics.Matrix4x4;
using PrimitiveType = Assimp.PrimitiveType;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace FLVER_Editor
{
    public partial class MainWindow : Form
    {
        private const int mtEditButtonIndex = 5;
        private const int mtViewerHighlightButtonIndex = 6;
        private const int mtAddPresetCbIndex = 7;
        private const int mtDeletePresetCbIndex = 8;
        private const int mtApplyPresetCbIndex = 9;
        private const int mtDeleteCbIndex = 10;
        private const string imageFilesFilter = "DDS File (*.dds)|*.dds";
        private const string version = "1.73";
        public static List<string> arguments;
        private static FLVER flver;
        private static BND4 flverBnd;
        private static BND4 matBinBnd;
        private static string flverFilePath;
        private static string matBinBndPath;
        private static DCX.Type flverArchiveType;
        private static int currFlverFileBinderIndex = -1;
        private static int dummyThickness = 5;
        private static Mono3D viewer;
        private static Color tabWindowBackColor = DefaultBackColor;
        private static Color tabWindowForeColor = DefaultForeColor;
        private static readonly List<int> selectedMeshIndices = new List<int>();
        private static readonly List<int> selectedDummyIndices = new List<int>();
        private static readonly List<int> selectedMaterialMeshIndices = new List<int>();
        private static readonly List<Vector3D> bonePositionList = new List<Vector3D>();
        private static Dictionary<object, object> materialPresets;
        private static Dictionary<object, object> dummyPresets;
        public static RotationOrder rotOrder = RotationOrder.YZX;
        public static readonly string rootFolderPath = Path.GetDirectoryName(Application.ExecutablePath);
        private static string materialPresetsFilePath = $"{rootFolderPath}/mpresets.json";
        private static string dummyPresetsFilePath = $"{rootFolderPath}/dpresets.json";
        private static readonly string mPresetsConfigFilePath = $"{rootFolderPath}/mpresetsconfig.txt";
        private static readonly string dPresetsConfigFilePath = $"{rootFolderPath}/dpresetsconfig.txt";
        private static readonly string matBinBndConfigPath = $"{rootFolderPath}/matbinbndconfig.txt";
        private static readonly string matTableViewConfigPath = $"{rootFolderPath}/mattableviewconfig.txt";
        private static readonly string dummyThicknessConfigPath = $"{rootFolderPath}/dummythicknessconfig.txt";
        private static readonly string autoSaveIntervalConfigPath = $"{rootFolderPath}/autosaveintervalconfig.txt";
        private static readonly string autoSaveEnabledConfigPath = $"{rootFolderPath}/autosaveenabledconfig.txt";
        private static bool meshIsSelected;
        private static bool dummyIsSelected;
        private static bool isSettingDefaultInfo = true;
        public static bool textureRefreshEnabled = true;
        private static int selectedMaterialIndex = -1;
        private static float prevNumVal;

        public MainWindow()
        {
            InitializeComponent();
            CheckForUpdates();
            GloballyDisableDataTableColumnSorting();
            SetMaterialsTableView();
            SetDummyThickness();
            SetAutoSaveInterval();
            SetAutoSaveEnabled();
            EnableDarkTheme();
            tabWindow.SelectedTab = meshTabPage;
            if (!OpenFLVERFile()) Environment.Exit(Environment.ExitCode);
        }

        private void SetAutoSaveEnabled()
        {
            try
            {
                autoSaveTimer.Enabled = !bool.Parse(File.ReadAllText(autoSaveEnabledConfigPath));
                ToggleAutoSaveState(false, false);
            }
            catch { }
        }

        private void ToggleAutoSaveState(bool showInfo, bool writeToConfig)
        {
            if (autoSaveTimer.Enabled)
            {
                if (showInfo) ShowInformationDialog("AutoSave is now disabled!");
                autoSaveTimer.Enabled = false;
                autoSaveTimer.Stop();
            }
            else
            {
                if (showInfo) ShowInformationDialog("AutoSave is now enabled, which will now save your work automatically!");
                autoSaveTimer.Enabled = true;
                autoSaveTimer.Start();
            }
            if (writeToConfig) File.WriteAllText(autoSaveEnabledConfigPath, autoSaveTimer.Enabled.ToString().ToLower());
        }

        private void SetAutoSaveInterval()
        {
            try
            {
                CheckAutoSaveInterval(File.ReadAllText(autoSaveIntervalConfigPath));
            }
            catch { }
            autoSaveIntervalSelector.Text = (autoSaveTimer.Interval / 60000).ToString();
        }

        private void SetDummyThickness()
        {
            try
            {
                dummyThickness = int.Parse(File.ReadAllText(dummyThicknessConfigPath));
                if (dummyThickness > dummyThicknessSelector.Items.Count) dummyThickness = 5;
            }
            catch { }
            dummyThicknessSelector.SelectedIndex = dummyThickness - 1;
        }

        private void SetMaterialsTableView()
        {
            try
            {
                materialsPagePanelsContainer.SplitterDistance = int.Parse(File.ReadAllText(matTableViewConfigPath));
            }
            catch { }
        }

        private void ChangeTheme(Control control, Color backColor, Color foreColor)
        {
            tabWindowBackColor = backColor;
            tabWindowForeColor = foreColor;
            tabWindow.Refresh();
            foreach (Control subControl in control.Controls)
            {
                if (subControl == xLeftRightLabel
                    || subControl == xHorizontalLabel
                    || subControl == xSwivelLabel
                    || subControl == yUpDownLabel
                    || subControl == yVerticalLabel
                    || subControl == yRollLabel
                    || subControl == zInOutLabel
                    || subControl == zThicknessLabel
                    || subControl == zCartwheelLabel) continue;
                switch (subControl)
                {
                    case DataGridView table:
                        table.ColumnHeadersDefaultCellStyle.BackColor = backColor;
                        table.ColumnHeadersDefaultCellStyle.ForeColor = foreColor;
                        table.EnableHeadersVisualStyles = false;
                        table.BackgroundColor = backColor;
                        table.DefaultCellStyle.BackColor = backColor;
                        table.DefaultCellStyle.ForeColor = foreColor;
                        break;
                    case ComboBox box:
                        box.FlatStyle = FlatStyle.Popup;
                        break;
                    case Button button:
                        button.FlatStyle = FlatStyle.Flat;
                        button.FlatAppearance.BorderSize = 1;
                        break;
                    case CheckBox box:
                        box.FlatStyle = FlatStyle.Flat;
                        box.FlatAppearance.BorderSize = 1;
                        break;
                }
                subControl.BackColor = backColor;
                subControl.ForeColor = foreColor;
                ChangeTheme(subControl, backColor, foreColor);
            }
        }

        private void EnableDarkTheme()
        {
            ChangeTheme(this, ColorTranslator.FromHtml("#323232"), ColorTranslator.FromHtml("#d9d9d9"));
        }

        private void CheckForUpdates()
        {
            versionStr.Text += $@" {version}";
            try
            {
                var client = new WebClient();
                if (client.DownloadString("https://pastebin.com/raw/9f04Uf1i").Contains(version))
                {
                    if (File.Exists($"{rootFolderPath}/MySFformat.bak")) File.Delete($"{rootFolderPath}/MySFformat.bak");
                    return;
                }
                if (ShowQuestionDialog("A new version of FLVER Editor is available, would you like to update?") != DialogResult.Yes) return;
                var zipPath = $"{rootFolderPath}/MySFformat.zip";
                client.DownloadFile("https://flvereditor2.000webhostapp.com/update/MySFformat.zip", zipPath);
                ZipFile.ExtractToDirectory(zipPath, $"{rootFolderPath}/update/");
                if (File.Exists($"{rootFolderPath}/MySFformat.zip")) File.Delete($"{rootFolderPath}/MySFformat.zip");
                File.Move($"{rootFolderPath}/MySFformat.exe", $"{rootFolderPath}/MySFformat.bak");
                File.Copy($"{rootFolderPath}/update/MySFformat.exe", $"{rootFolderPath}/MySFformat.exe", true);
                if (Directory.Exists($"{rootFolderPath}/update/")) Directory.Delete($"{rootFolderPath}/update/", true);
                Process.Start(Application.ExecutablePath, arguments[0]);
                Environment.Exit(Environment.ExitCode);
            }
            catch { }
        }

        private static Vector3 ConvertSysNumToXnaVector3(System.Numerics.Vector3 v) { return new Vector3(v.X, v.Y, v.Z); }

        private static Vector3 ConvertSysNumToXnaVector3Flipped(System.Numerics.Vector3 v) { return new Vector3(v.X, v.Z, v.Y); }

        private static Vector3 VectorCrossProduct(Vector3 v1, Vector3 v2)
        {
            return new Vector3(v1.Y * v2.Z - v1.Z * v2.Y, v1.Z * v2.X - v1.X * v2.Z, v1.X * v2.Y - v1.Y * v2.X);
        }

        private static float VectorDotProduct(Vector3 v1, Vector3 v2) { return v1.X * v2.X + v1.Y * v2.Y + v1.Z * v2.Z; }

        public static void UpdateMesh()
        {
            var vertexPosColorList = new List<VertexPositionColor>();
            var faceSetPosColorList = new List<VertexPositionColor>();
            var faceSetPosColorTexList = new List<VertexPositionColorTexture>();
            var vertexTexMapList = new List<VertexTexMap>();
            for (var i = 0; i < flver.Meshes.Count; ++i)
            {
                if (flver.Meshes[i] == null) continue;
                bool renderBackFaces = flver.Meshes[i].FaceSets.Count > 0 && !flver.Meshes[i].FaceSets[0].CullBackfaces;
                foreach (FLVER.Vertex[] vertexArr in flver
                             .Meshes[i]
                             .GetFaces())
                {
                    Microsoft.Xna.Framework.Color colorLine = Microsoft.Xna.Framework.Color.Black;
                    if (meshIsSelected && selectedMeshIndices.IndexOf(i) != -1)
                    {
                        colorLine.R = colorLine.G = 255;
                    }
                    else if (selectedMaterialMeshIndices.IndexOf(i) != -1)
                    {
                        colorLine.G = 0;
                        colorLine.R = colorLine.B = 255;
                    }
                    colorLine.A = 125;
                    vertexPosColorList.AddRange(new[]
                    {
                        new VertexPositionColor(ConvertSysNumToXnaVector3Flipped(vertexArr[0].Positions[0]), colorLine),
                        new VertexPositionColor(ConvertSysNumToXnaVector3Flipped(vertexArr[1].Positions[0]), colorLine),
                        new VertexPositionColor(ConvertSysNumToXnaVector3Flipped(vertexArr[0].Positions[0]), colorLine),
                        new VertexPositionColor(ConvertSysNumToXnaVector3Flipped(vertexArr[2].Positions[0]), colorLine),
                        new VertexPositionColor(ConvertSysNumToXnaVector3Flipped(vertexArr[1].Positions[0]), colorLine),
                        new VertexPositionColor(ConvertSysNumToXnaVector3Flipped(vertexArr[2].Positions[0]), colorLine)
                    });
                    var faceSetColor = new Microsoft.Xna.Framework.Color();
                    Vector3 vectorA
                        = ConvertSysNumToXnaVector3(vertexArr[1].Positions[0]) - ConvertSysNumToXnaVector3(vertexArr[0].Positions[0]);
                    Vector3 vectorB
                        = ConvertSysNumToXnaVector3(vertexArr[2].Positions[0]) - ConvertSysNumToXnaVector3(vertexArr[0].Positions[0]);
                    Vector3 normalVector = VectorCrossProduct(vectorA, vectorB);
                    normalVector.Normalize();
                    var lightVector = new Vector3(viewer.lightX, viewer.lightY, viewer.lightZ);
                    lightVector.Normalize();
                    int faceSetColorVal = 125 + (int)(125 * VectorDotProduct(normalVector, lightVector));
                    faceSetColorVal = faceSetColorVal > 255 ? 255 : faceSetColorVal < 0 ? 0 : viewer.flatShading ? 255 : faceSetColorVal;
                    faceSetColor.R = faceSetColor.G = faceSetColor.B = (byte)faceSetColorVal;
                    faceSetColor.A = 255;
                    if (meshIsSelected && selectedMeshIndices.IndexOf(i) != -1)
                    {
                        faceSetColor.B = 0;
                    }
                    else if (selectedMaterialMeshIndices.IndexOf(i) != -1)
                    {
                        faceSetColor.B = 255;
                        faceSetColor.G = 0;
                    }
                    faceSetPosColorList.AddRange(
                        new[]
                        {
                            new VertexPositionColor(ConvertSysNumToXnaVector3Flipped(vertexArr[0].Positions[0]), faceSetColor),
                            new VertexPositionColor(ConvertSysNumToXnaVector3Flipped(vertexArr[2].Positions[0]), faceSetColor),
                            new VertexPositionColor(ConvertSysNumToXnaVector3Flipped(vertexArr[1].Positions[0]), faceSetColor),
                            new VertexPositionColor(ConvertSysNumToXnaVector3Flipped(vertexArr[0].Positions[0]), faceSetColor),
                            new VertexPositionColor(ConvertSysNumToXnaVector3Flipped(vertexArr[2].Positions[0]), faceSetColor),
                            new VertexPositionColor(ConvertSysNumToXnaVector3Flipped(vertexArr[1].Positions[0]), faceSetColor)
                        });
                    faceSetPosColorTexList.AddRange(
                        new[]
                        {
                            new VertexPositionColorTexture(ConvertSysNumToXnaVector3Flipped(vertexArr[0].Positions[0]), faceSetColor,
                                new Microsoft.Xna.Framework.Vector2(vertexArr[0].UVs[0].X, vertexArr[0].UVs[0].Y)),
                            new VertexPositionColorTexture(ConvertSysNumToXnaVector3Flipped(vertexArr[2].Positions[0]), faceSetColor,
                                new Microsoft.Xna.Framework.Vector2(vertexArr[2].UVs[0].X, vertexArr[2].UVs[0].Y)),
                            new VertexPositionColorTexture(ConvertSysNumToXnaVector3Flipped(vertexArr[1].Positions[0]), faceSetColor,
                                new Microsoft.Xna.Framework.Vector2(vertexArr[1].UVs[0].X, vertexArr[1].UVs[0].Y))
                        });
                    if (!renderBackFaces) continue;
                    faceSetPosColorList.AddRange(
                        new[]
                        {
                            new VertexPositionColor(ConvertSysNumToXnaVector3Flipped(vertexArr[0].Positions[0]), faceSetColor),
                            new VertexPositionColor(ConvertSysNumToXnaVector3Flipped(vertexArr[1].Positions[0]), faceSetColor),
                            new VertexPositionColor(ConvertSysNumToXnaVector3Flipped(vertexArr[2].Positions[0]), faceSetColor)
                        });
                    faceSetPosColorTexList.AddRange(
                        new[]
                        {
                            new VertexPositionColorTexture(ConvertSysNumToXnaVector3Flipped(vertexArr[0].Positions[0]), faceSetColor,
                                new Microsoft.Xna.Framework.Vector2(vertexArr[0].UVs[0].X, vertexArr[0].UVs[0].Y)),
                            new VertexPositionColorTexture(ConvertSysNumToXnaVector3Flipped(vertexArr[1].Positions[0]), faceSetColor,
                                new Microsoft.Xna.Framework.Vector2(vertexArr[1].UVs[0].X, vertexArr[1].UVs[0].Y)),
                            new VertexPositionColorTexture(ConvertSysNumToXnaVector3Flipped(vertexArr[2].Positions[0]), faceSetColor,
                                new Microsoft.Xna.Framework.Vector2(vertexArr[2].UVs[0].X, vertexArr[2].UVs[0].Y))
                        });
                }
                for (var j = 0; j < flver.Meshes[i].Vertices.Count; ++j)
                {
                    Program.vertices.Add(flver.Meshes[i].Vertices[j]);
                    Program.verticesInfo.Add(new VertexInfo { meshIndex = i, vertexIndex = (uint)j });
                }
                var vertexTexMap = new VertexTexMap
                {
                    textureName
                        = Path.GetFileNameWithoutExtension(flver.Materials[flver.Meshes[i].MaterialIndex].Textures[0].Path),
                    faceSetTextures = faceSetPosColorTexList.ToArray()
                };
                faceSetPosColorTexList.Clear();
                vertexTexMapList.Add(vertexTexMap);
            }
            if (vertexPosColorList.Count % 2 != 0) vertexPosColorList.Add(vertexPosColorList[vertexPosColorList.Count - 1]);
            for (var i = 0; i < bonePositionList.Count; ++i)
                bonePositionList[i] = null;
            var bonesTransform = new Transform3D[flver.Bones.Count];
            for (var i = 0; i < flver.Bones.Count; ++i)
            {
                bonesTransform[i] = new Transform3D { rotOrder = rotOrder, position = new Vector3D(flver.Bones[i].Translation) };
                bonesTransform[i].setRotationInRad(new Vector3D(flver.Bones[i].Rotation));
                bonesTransform[i].scale = new Vector3D(flver.Bones[i].Scale);
                if (flver.Bones[i].ParentIndex < 0) continue;
                bonesTransform[i].parent = bonesTransform[flver.Bones[i].ParentIndex];
                Vector3D absolutePos = bonesTransform[i].getGlobalOrigin();
                if (bonesTransform[flver.Bones[i].ParentIndex] == null) continue;
                Vector3D parentPos = bonesTransform[flver.Bones[i].ParentIndex].getGlobalOrigin();
                vertexPosColorList.Add(new VertexPositionColor(new Vector3(parentPos.X - 0.005f, parentPos.Z - 0.005f, parentPos.Y),
                    Microsoft.Xna.Framework.Color.Purple));
                vertexPosColorList.Add(new VertexPositionColor(new Vector3(absolutePos.X, absolutePos.Z, absolutePos.Y),
                    Microsoft.Xna.Framework.Color.Purple));
                vertexPosColorList.Add(new VertexPositionColor(new Vector3(parentPos.X + 0.005f, parentPos.Z + 0.005f, parentPos.Y),
                    Microsoft.Xna.Framework.Color.Purple));
                vertexPosColorList.Add(new VertexPositionColor(new Vector3(absolutePos.X, absolutePos.Z, absolutePos.Y),
                    Microsoft.Xna.Framework.Color.Purple));
            }
            for (var i = 0; i < flver.Dummies.Count; ++i)
            {
                FLVER.Dummy dummy = flver.Dummies[i];
                bool shouldSelectDummy = dummyIsSelected && selectedDummyIndices.IndexOf(i) != -1;
                Microsoft.Xna.Framework.Color dummyColor = shouldSelectDummy ? Microsoft.Xna.Framework.Color.Yellow : Microsoft.Xna.Framework.Color.Purple;
                float baseDummyYPos = dummy.Position.Y;
                const float posStep = 0.0005f;
                for (var j = 0; j < dummyThickness; ++j)
                {
                    vertexPosColorList.AddRange(new[]
                    {
                        new VertexPositionColor(new Vector3(dummy.Position.X - 0.025f, dummy.Position.Z, baseDummyYPos), dummyColor),
                        new VertexPositionColor(new Vector3(dummy.Position.X + 0.025f, dummy.Position.Z, baseDummyYPos), dummyColor),
                        new VertexPositionColor(new Vector3(dummy.Position.X, dummy.Position.Z - 0.025f, baseDummyYPos), dummyColor),
                        new VertexPositionColor(new Vector3(dummy.Position.X, dummy.Position.Z + 0.025f, baseDummyYPos), dummyColor),
                        new VertexPositionColor(new Vector3(dummy.Position.X, dummy.Position.Z, baseDummyYPos), Microsoft.Xna.Framework.Color.Green),
                        new VertexPositionColor(new Vector3(dummy.Position.X + dummy.Forward.X, dummy.Position.Z + dummy.Forward.Z,
                                baseDummyYPos + dummy.Forward.Y),
                            Microsoft.Xna.Framework.Color.Green)
                    });
                    baseDummyYPos -= posStep;
                }
            }
            if (Program.useCheckingPoint)
            {
                System.Numerics.Vector3 checkingPoint = Program.checkingPoint;
                System.Numerics.Vector3 checkingPointNormal = Program.checkingPointNormal;
                vertexPosColorList.Add(new VertexPositionColor(new Vector3(checkingPoint.X - 0.05f, checkingPoint.Z - 0.05f, checkingPoint.Y),
                    Microsoft.Xna.Framework.Color.AntiqueWhite));
                vertexPosColorList.Add(new VertexPositionColor(new Vector3(checkingPoint.X + 0.05f, checkingPoint.Z + 0.05f, checkingPoint.Y),
                    Microsoft.Xna.Framework.Color.AntiqueWhite));
                vertexPosColorList.Add(new VertexPositionColor(new Vector3(checkingPoint.X - 0.05f, checkingPoint.Z + 0.05f, checkingPoint.Y),
                    Microsoft.Xna.Framework.Color.AntiqueWhite));
                vertexPosColorList.Add(new VertexPositionColor(new Vector3(checkingPoint.X + 0.05f, checkingPoint.Z - 0.05f, checkingPoint.Y),
                    Microsoft.Xna.Framework.Color.AntiqueWhite));
                vertexPosColorList.Add(new VertexPositionColor(new Vector3(checkingPoint.X, checkingPoint.Z, checkingPoint.Y), Microsoft.Xna.Framework.Color.Blue));
                vertexPosColorList.Add(new VertexPositionColor(
                    new Vector3(checkingPoint.X + 0.2f * checkingPointNormal.X, checkingPoint.Z + 0.2f * checkingPointNormal.Z, checkingPoint.Y + 0.2f * checkingPointNormal.Y),
                    Microsoft.Xna.Framework.Color.Blue));
            }
            viewer.vertices = vertexPosColorList.ToArray();
            viewer.vertexTexMapList = vertexTexMapList.ToArray();
            viewer.faceSets = faceSetPosColorList.ToArray();
        }

        private static void ClearViewerMaterialHighlight()
        {
            selectedMaterialMeshIndices.Clear();
        }

        private void DeselectAllSelectedThings()
        {
            isSettingDefaultInfo = true;
            ResetModifierNumBoxValues();
            meshIsSelected = false;
            dummyIsSelected = false;
            selectedMeshIndices.Clear();
            selectedDummyIndices.Clear();
            meshModifiersContainer.Enabled = false;
            isSettingDefaultInfo = false;
        }

        private static void LoadViewer()
        {
            if (viewer == null)
            {
                new Thread(() =>
                {
                    Thread.CurrentThread.IsBackground = true;
                    viewer = new Mono3D();
                    UpdateMesh();
                    viewer.RefreshTextures();
                    viewer.Run();
                }).Start();
                return;
            }
            UpdateMesh();
            viewer.RefreshTextures();
        }

        private static bool LoadPresets(ComboBox selector, ref Dictionary<object, object> dict, string filePath)
        {
            selector.Items.Clear();
            var hasRead = true;
            try
            {
                dict = new JavaScriptSerializer().Deserialize<Dictionary<object, object>>(File.ReadAllText(filePath));
                selector.Items.AddRange(dict.Keys.ToArray());
            }
            catch
            {
                hasRead = false;
            }
            return hasRead;
        }

        private static void CheckForPresetsConfig(string configPath, ref string presetsPath)
        {
            if (!File.Exists(configPath)) return;
            string filePath = File.ReadAllText(configPath);
            if (filePath != "") presetsPath = filePath;
        }

        private void LoadMaterialPresets()
        {
            CheckForPresetsConfig(mPresetsConfigFilePath, ref materialPresetsFilePath);
            bool hasRead = LoadPresets(materialPresetsSelector, ref materialPresets, materialPresetsFilePath);
            materialPresetsSelector.Enabled = applyPresetToAllMaterialsButton.Enabled
                = materialsTable.Columns[mtApplyPresetCbIndex].Visible =
                    materialsTable.Columns[mtAddPresetCbIndex].Visible = materialsTable.Columns[mtDeletePresetCbIndex].Visible = hasRead;
        }

        private void LoadDummyPresets()
        {
            CheckForPresetsConfig(dPresetsConfigFilePath, ref dummyPresetsFilePath);
            dummyPresetsSelector.Enabled = dummiesTableOKButton.Enabled =
                addAllDummiesToPresetsButton.Enabled = LoadPresets(dummyPresetsSelector, ref dummyPresets, dummyPresetsFilePath);
        }

        private static void DisableDataTableColumnSorting(DataGridView table)
        {
            foreach (DataGridViewColumn column in table.Columns)
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
        }

        private void GloballyDisableDataTableColumnSorting()
        {
            DisableDataTableColumnSorting(bonesTable);
            DisableDataTableColumnSorting(materialsTable);
            DisableDataTableColumnSorting(texturesTable);
            DisableDataTableColumnSorting(meshTable);
            DisableDataTableColumnSorting(dummiesTable);
        }

        private void UpdateUI()
        {
            isSettingDefaultInfo = true;
            applyMatBinTexturesButton.Enabled = false;
            bonesTable.Rows.Clear();
            materialsTable.Rows.Clear();
            texturesTable.Rows.Clear();
            meshTable.Rows.Clear();
            dummiesTable.Rows.Clear();
            for (var i = 0; i < flver.Bones.Count; ++i)
            {
                FLVER.Bone bone = flver.Bones[i];
                var row = new DataGridViewRow();
                row.Cells.AddRange(new DataGridViewTextBoxCell { Value = i }, new DataGridViewTextBoxCell { Value = bone.Name },
                    new DataGridViewTextBoxCell { Value = bone.ParentIndex }, new DataGridViewTextBoxCell { Value = bone.ChildIndex },
                    new DataGridViewTextBoxCell { Value = $"{bone.Translation.X},{bone.Translation.Y},{bone.Translation.Z}" },
                    new DataGridViewTextBoxCell { Value = $"{bone.Scale.X},{bone.Scale.Y},{bone.Scale.Z}" },
                    new DataGridViewTextBoxCell { Value = $"{bone.Rotation.X},{bone.Rotation.Y},{bone.Rotation.Z}" },
                    new DataGridViewTextBoxCell { Value = $"{bone.BoundingBoxMin.X},{bone.BoundingBoxMin.Y},{bone.BoundingBoxMin.Z}" },
                    new DataGridViewTextBoxCell { Value = $"{bone.BoundingBoxMax.X},{bone.BoundingBoxMax.Y},{bone.BoundingBoxMax.Z}" });
                bonesTable.Rows.Add(row);
            }
            for (var i = 0; i < flver.Materials.Count; ++i)
            {
                FLVER.Material material = flver.Materials[i];
                var row = new DataGridViewRow();
                material.Unk18 = i;
                row.Cells.AddRange(new DataGridViewTextBoxCell { Value = i }, new DataGridViewTextBoxCell { Value = material.Name },
                    new DataGridViewTextBoxCell { Value = material.Flags },
                    new DataGridViewTextBoxCell { Value = material.MTD }, new DataGridViewTextBoxCell { Value = material.Unk18 });
                for (var j = 0; j < 4; ++j)
                    row.Cells.Add(new DataGridViewButtonCell { Value = "" });
                for (var j = 0; j < 2; ++j)
                    row.Cells.Add(new DataGridViewCheckBoxCell { Value = false });
                materialsTable.Rows.Add(row);
            }
            for (var i = 0; i < flver.Meshes.Count; ++i)
            {
                FLVER.Mesh mesh = flver.Meshes[i];
                if (mesh.MaterialIndex < 0) mesh.MaterialIndex = 0;
                var row = new DataGridViewRow();
                row.Cells.AddRange(new DataGridViewTextBoxCell { Value = i },
                    new DataGridViewTextBoxCell { Value = flver.Materials[mesh.MaterialIndex].Name },
                    new DataGridViewTextBoxCell { Value = flver.Meshes[i].Vertices[0].BoneIndices?[0] });
                row.Cells.Add(new DataGridViewCheckBoxCell { Value = false });
                meshTable.Rows.Add(row);
            }
            for (var i = 0; i < flver.Dummies.Count; ++i)
            {
                FLVER.Dummy dummy = flver.Dummies[i];
                var row = new DataGridViewRow();
                row.Cells.AddRange(new DataGridViewTextBoxCell { Value = i },
                    new DataGridViewTextBoxCell { Value = dummy.ReferenceID },
                    new DataGridViewTextBoxCell { Value = dummy.AttachBoneIndex },
                    new DataGridViewTextBoxCell { Value = dummy.DummyBoneIndex });
                row.Cells.Add(new DataGridViewCheckBoxCell { Value = false });
                row.Cells.Add(new DataGridViewButtonCell { Value = "" });
                dummiesTable.Rows.Add(row);
            }
            isSettingDefaultInfo = false;
        }

        private static void BackupFLVERFile(string filePath)
        {
            string backupFilePath = filePath.Replace(".flver", ".flver.bak").Replace(".dcx", ".dcx.bak");
            if (!File.Exists(backupFilePath)) File.Copy(flverFilePath, backupFilePath);
        }

        private static void UpdateWindowTitle()
        {
            Program.window.Text = $@"{Program.windowTitle} - {Path.GetFileName(flverFilePath)}";
        }

        private static string PromptFLVERModel()
        {
            var dialog = new OpenFileDialog
            {
                Filter =
                    @"FLVER File (*.flver, *.flver.bak)|*.flver;*.flver.bak|BND File (*.dcx, *.dcx.bak)|*.dcx;*.dcx.bak|Model Container (*.flver, *.flver.bak, *.dcx, *.dcx.bak)|*.flver;*.flver.bak;*.dcx;*.dcx.bak",
                FilterIndex = 3, Multiselect = false
            };
            return dialog.ShowDialog() != DialogResult.OK ? "" : dialog.FileName.ToLower();
        }

        private static bool IsFLVERPath(string filePath)
        {
            return filePath.EndsWith(".flver") || filePath.EndsWith(".flver.bak");
        }

        private static FLVER ReadFLVERFromDCXPath(string filePath, bool setMainFlverArchiveType, bool setBinderIndex, bool wantsTpf)
        {
            var flverFiles = new List<BinderFile>();
            try
            {
                flverBnd = SoulsFile<BND4>.Read(filePath);
            }
            catch
            {
                flverBnd = SoulsFile<BND4>.Read(DCX.Decompress(filePath));
            }
            if (setMainFlverArchiveType) flverArchiveType = flverBnd.Compression;
            var binderIndex = 0;
            foreach (BinderFile file in flverBnd.Files)
            {
                if (file.Name.EndsWith(".flver"))
                {
                    flverFiles.Add(file);
                    if (currFlverFileBinderIndex == -1 && setBinderIndex) currFlverFileBinderIndex = binderIndex;
                }
                else if (file.Name.EndsWith(".tpf") && wantsTpf)
                {
                    try
                    {
                        Program.tpf = TPF.Read(file.Bytes);
                    }
                    catch
                    {
                        ShowErrorDialog($"The TPF file {file.Name} is unsupported.");
                    }
                }
                binderIndex++;
            }
            if (flverFiles.Count > 0)
            {
                FLVER currFlver = FLVER.Read(flverFiles[0].Bytes);
                return currFlver;
            }
            ShowInformationDialog("No FLVER files were found in the DCX archive.");
            return new FLVER();
        }

        private bool OpenFLVERFile()
        {
            isSettingDefaultInfo = true;
            if (arguments.Count > 0)
            {
                flverFilePath = arguments[0].ToLower();
                arguments = new List<string>();
            }
            else
            {
                flverFilePath = PromptFLVERModel();
                if (flverFilePath == "") return false;
            }
            if (Program.window != null) UpdateWindowTitle();
            Program.filePath = flverFilePath;
            if (IsFLVERPath(flverFilePath))
            {
                flver = FLVER.Read(flverFilePath);
                Program.flver = flver;
            }
            else
            {
                flver = ReadFLVERFromDCXPath(flverFilePath, true, true, true);
                Program.flver = flver;
            }
            saveToolStripMenuItem.Enabled = saveAsToolStripMenuItem.Enabled = true;
            matBinBndPath = null;
            UpdateUI();
            DeselectAllSelectedThings();
            ClearViewerMaterialHighlight();
            LoadMaterialPresets();
            LoadDummyPresets();
            LoadViewer();
            isSettingDefaultInfo = false;
            return true;
        }

        private void OpenButtonClicked(object sender, EventArgs e)
        {
            OpenFLVERFile();
        }

        private void UpdateTexturesTable()
        {
            if (selectedMaterialIndex == -1) return;
            texturesTable.Rows.Clear();
            for (var i = 0; i < flver.Materials[selectedMaterialIndex].Textures.Count; ++i)
            {
                FLVER.Material material = flver.Materials[selectedMaterialIndex];
                var row = new DataGridViewRow();
                row.Cells.AddRange(new DataGridViewTextBoxCell { Value = material.Textures[i].Type },
                    new DataGridViewTextBoxCell { Value = material.Textures[i].Path });
                row.Cells.Add(new DataGridViewButtonCell { Value = "" });
                texturesTable.Rows.Add(row);
            }
        }

        private void UpdateMaterialPresets()
        {
            File.WriteAllText(materialPresetsFilePath, JToken.Parse(new JavaScriptSerializer().Serialize(materialPresets)).ToString());
            LoadMaterialPresets();
        }

        private void MaterialsTableButtonClicked(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            switch (e.ColumnIndex)
            {
                case mtEditButtonIndex:
                    applyMatBinTexturesButton.Enabled = true;
                    selectedMaterialIndex = e.RowIndex;
                    UpdateTexturesTable();
                    break;
                case mtViewerHighlightButtonIndex:
                    bool unhighlighted = flver.Meshes.Any(mesh => selectedMaterialMeshIndices.IndexOf(flver.Meshes.IndexOf(mesh)) == -1 && mesh.MaterialIndex == e.RowIndex);
                    ClearViewerMaterialHighlight();
                    if (unhighlighted)
                    {
                        foreach (FLVER.Mesh mesh in flver.Meshes.Where(mesh => mesh.MaterialIndex == e.RowIndex))
                            selectedMaterialMeshIndices.Add(flver.Meshes.IndexOf(mesh));
                    }
                    UpdateMesh();
                    break;
                case mtAddPresetCbIndex when !materialPresets.ContainsKey(flver.Materials[e.RowIndex].Name):
                    materialPresets.Add(flver.Materials[e.RowIndex].Name, flver.Materials[e.RowIndex]);
                    UpdateMaterialPresets();
                    break;
                case mtDeletePresetCbIndex when materialPresets.ContainsKey(flver.Materials[e.RowIndex].Name):
                    materialPresets.Remove(flver.Materials[e.RowIndex].Name);
                    UpdateMaterialPresets();
                    break;
            }
        }

        private static bool AreCheckboxesInDataTableAllChecked(DataGridView table, int columnIndex)
        {
            var allChecked = true;
            foreach (DataGridViewRow row in table.Rows)
                if (!(bool)row.Cells[columnIndex].Value)
                    allChecked = false;
            return allChecked;
        }

        private static void ToggleCheckboxesInDataTable(DataGridView table, int columnIndex)
        {
            bool allChecked = AreCheckboxesInDataTableAllChecked(table, columnIndex);
            foreach (DataGridViewRow row in table.Rows)
                row.Cells[columnIndex].Value = !allChecked;
        }

        private void MaterialsTableApplyToAllButtonClicked(object sender, MouseEventArgs e)
        {
            ToggleCheckboxesInDataTable(materialsTable, mtApplyPresetCbIndex);
        }

        private void MaterialsTableDeleteAllButtonClicked(object sender, MouseEventArgs e)
        {
            ToggleCheckboxesInDataTable(materialsTable, mtDeleteCbIndex);
        }

        private void TexturesTableButtonClicked(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex != 2) return;
            var dialog = new OpenFileDialog { Filter = imageFilesFilter };
            if (dialog.ShowDialog() != DialogResult.OK) return;
            flver.Materials[selectedMaterialIndex].Textures[e.RowIndex].Path = $"{Path.GetFileNameWithoutExtension(dialog.FileName)}.tif";
            UpdateTexturesTable();
            UpdateMesh();
            viewer.RefreshTextures();
        }

        private void ResetModifierNumBoxValues()
        {
            foreach (NumericUpDown box in meshModifiersNumBoxesContainer.Controls)
                box.Value = 0;
        }

        private void EnableDisableExtraModifierOptions()
        {
            reverseFacesetsCheckbox.Enabled = reverseNormalsCheckbox.Enabled = toggleBackfacesCheckbox.Enabled =
                deleteFacesetsCheckbox.Enabled = uniformScaleCheckbox.Enabled = centerToWorldButton.Enabled = mirrorXCheckbox.Enabled =
                    mirrorYCheckbox.Enabled = mirrorZCheckbox.Enabled = selectedMeshIndices.Count != 0;
        }

        private void UpdateSelectedMeshIndices(int columnIndex, int rowIndex)
        {
            if (columnIndex != 3 || rowIndex < 0) return;
            if ((bool)meshTable[columnIndex, rowIndex].Value)
            {
                if (selectedMeshIndices.IndexOf(rowIndex) == -1) selectedMeshIndices.Add(rowIndex);
                else selectedMeshIndices.RemoveAt(selectedMeshIndices.IndexOf(rowIndex));
            }
            else
            {
                if (selectedMeshIndices.Count < 1) meshIsSelected = true;
                if (selectedMeshIndices.IndexOf(rowIndex) == -1) selectedMeshIndices.Add(rowIndex);
                else selectedMeshIndices.RemoveAt(selectedMeshIndices.IndexOf(rowIndex));
            }
        }

        private void UpdateSelectedDummyIndices(int columnIndex, int rowIndex)
        {
            if (columnIndex != 4 || rowIndex < 0) return;
            if ((bool)dummiesTable[columnIndex, rowIndex].Value)
            {
                if (selectedDummyIndices.IndexOf(rowIndex) == -1) selectedDummyIndices.Add(rowIndex);
                else selectedDummyIndices.RemoveAt(selectedDummyIndices.IndexOf(rowIndex));
            }
            else
            {
                if (selectedDummyIndices.Count < 1) dummyIsSelected = true;
                if (selectedDummyIndices.IndexOf(rowIndex) == -1) selectedDummyIndices.Add(rowIndex);
                else selectedDummyIndices.RemoveAt(selectedDummyIndices.IndexOf(rowIndex));
            }
        }

        private void UpdateSelectedDummies()
        {
            if (isSettingDefaultInfo) return;
            if (dummyIsSelected)
            {
                isSettingDefaultInfo = true;
                ResetModifierNumBoxValues();
                bool hasIndices = selectedDummyIndices.Count != 0;
                meshModifiersContainer.Enabled = hasIndices;
                if (hasIndices)
                {
                    EnableDisableExtraModifierOptions();
                    scaleXNumBox.Value = scaleYNumBox.Value = scaleZNumBox.Value = 100;
                    rotXNumBox.Value = rotYNumBox.Value = rotZNumBox.Value = 0;
                }
                isSettingDefaultInfo = false;
            }
            UpdateMesh();
        }

        private void UpdateSelectedMeshes()
        {
            if (isSettingDefaultInfo) return;
            if (meshIsSelected)
            {
                isSettingDefaultInfo = true;
                ResetModifierNumBoxValues();
                bool hasIndices = selectedMeshIndices.Count != 0;
                meshModifiersContainer.Enabled = hasIndices;
                if (hasIndices)
                {
                    EnableDisableExtraModifierOptions();
                    scaleXNumBox.Value = scaleYNumBox.Value = scaleZNumBox.Value = 100;
                    rotXNumBox.Value = rotYNumBox.Value = rotZNumBox.Value = 0;
                }
                isSettingDefaultInfo = false;
            }
            UpdateMesh();
        }

        private void MeshTableSelectCheckboxClicked(object sender, DataGridViewCellEventArgs e)
        {
            UpdateSelectedMeshIndices(e.ColumnIndex, e.RowIndex);
            UpdateSelectedMeshes();
        }

        private void DummiesTableSelectCheckboxClicked(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            switch (e.ColumnIndex)
            {
                case 4:
                    UpdateSelectedDummyIndices(e.ColumnIndex, e.RowIndex);
                    UpdateSelectedDummies();
                    break;
                case 5:
                    var duplicatedDummy = new FLVER.Dummy
                    {
                        Position = flver.Dummies[e.RowIndex].Position,
                        Forward = flver.Dummies[e.RowIndex].Forward,
                        ReferenceID = flver.Dummies[e.RowIndex].ReferenceID,
                        AttachBoneIndex = flver.Dummies[e.RowIndex].AttachBoneIndex,
                        DummyBoneIndex = flver.Dummies[e.RowIndex].DummyBoneIndex
                    };
                    flver.Dummies.Add(duplicatedDummy);
                    DeselectAllSelectedThings();
                    UpdateUI();
                    UpdateMesh();
                    break;
            }
        }

        private static System.Numerics.Vector3 CreateTranslationVector(float x, float y, float z, float offset, int nbi)
        {
            return new System.Numerics.Vector3(x + (nbi == 0 ? offset : 0), y + (nbi == 1 ? offset : 0), z + (nbi == 2 ? offset : 0));
        }

        private static System.Numerics.Vector3 CreateScaleVector(float x, float y, float z, float offset, IReadOnlyList<float> totals, int nbi, bool uniform, bool invert)
        {
            float scalar = offset < 0 && !invert ? -(offset - 1) : invert ? offset - 1 : offset + 1;
            float newX = nbi == 0 || uniform ? x - totals[0] : x;
            float newY = nbi == 1 || uniform ? y - totals[1] : y;
            float newZ = nbi == 2 || uniform ? z - totals[2] : z;
            newX = nbi == 0 || uniform ? (offset < 0 && !invert ? newX / scalar : newX * scalar) + totals[0] : x;
            newY = nbi == 1 || uniform ? (offset < 0 && !invert ? newY / scalar : newY * scalar) + totals[1] : y;
            newZ = nbi == 2 || uniform ? (offset < 0 && !invert ? newZ / scalar : newZ * scalar) + totals[2] : z;
            return new System.Numerics.Vector3(newX, newY, newZ);
        }

        private static dynamic CreateRotationVector(float x, float y, float z, float w, float offset, IReadOnlyList<float> totals, int nbi)
        {
            float newX = nbi == 1 ? offset : 0;
            float newY = nbi == 0 ? offset : 0;
            float newZ = nbi == 2 ? offset : 0;
            var vector = new System.Numerics.Vector3(x - totals[0], y - totals[1], z - totals[2]);
            vector = Program.RotatePoint(vector, newY, newX, newZ);
            return w == 0 ? (dynamic)new System.Numerics.Vector3(vector.X + totals[0], vector.Y + totals[1], vector.Z + totals[2]) :
                new Vector4(vector.X + totals[0], vector.Y + totals[1], vector.Z + totals[2], w);
        }

        private static void TranslateThing(dynamic thing, float offset, int nbi)
        {
            switch (thing)
            {
                case FLVER.Dummy d:
                    d.Position = CreateTranslationVector(d.Position.X, d.Position.Y, d.Position.Z, offset, nbi);
                    break;
                case FLVER.Vertex v:
                    v.Positions[0] = CreateTranslationVector(v.Positions[0].X, v.Positions[0].Y, v.Positions[0].Z, offset, nbi);
                    break;
            }
        }

        private static void ScaleThing(dynamic thing, float offset, IReadOnlyList<float> totals, int nbi, bool uniform, bool invert)
        {
            if (nbi >= 3 && nbi <= 5) nbi -= 3;
            switch (thing)
            {
                case FLVER.Dummy d:
                    if (selectedMeshIndices.Count != 0) d.Position = CreateScaleVector(d.Position.X, d.Position.Y, d.Position.Z, offset, totals, nbi, uniform, invert);
                    else d.Forward = CreateTranslationVector(d.Forward.X, d.Forward.Y, d.Forward.Z, offset, nbi);
                    break;
                case FLVER.Vertex v:
                    v.Positions[0] = CreateScaleVector(v.Positions[0].X, v.Positions[0].Y, v.Positions[0].Z, offset, totals, nbi, uniform, invert);
                    v.Normals[0] = new Vector4(v.Normals[0].X, v.Normals[0].Y, invert && nbi != 2 ? -v.Normals[0].Z : v.Normals[0].Z, v.Normals[0].W);
                    v.Tangents[0] = new Vector4(v.Tangents[0].X, v.Tangents[0].Y, invert && nbi != 2 ? -v.Normals[0].Z : v.Normals[0].Z, v.Tangents[0].W);
                    break;
            }
        }

        private static void RotateThing(dynamic thing, float offset, IReadOnlyList<float> totals, int nbi)
        {
            if (nbi >= 6 && nbi <= 8) nbi -= 6;
            float newX = nbi == 0 ? offset : 0;
            float newY = nbi == 1 ? offset : 0;
            float newZ = nbi == 2 ? offset : 0;
            switch (thing)
            {
                case FLVER.Dummy d:
                    if (selectedMeshIndices.Count != 0) d.Position = CreateRotationVector(d.Position.X, d.Position.Y, d.Position.Z, 0, offset, totals, nbi);
                    else d.Forward = Program.RotatePoint(d.Forward, newX, newZ, newY);
                    break;
                case FLVER.Vertex v:
                    v.Positions[0] = CreateRotationVector(v.Positions[0].X, v.Positions[0].Y, v.Positions[0].Z, 0, offset, totals, nbi);
                    v.Normals[0] = CreateRotationVector(v.Normals[0].X, v.Normals[0].Y, v.Normals[0].Z, v.Normals[0].W, offset, new float[3], nbi);
                    v.Tangents[0] = CreateRotationVector(v.Tangents[0].X, v.Tangents[0].Y, v.Tangents[0].Z, v.Tangents[0].W, offset, new float[3], nbi);
                    break;
            }
        }

        private void TransformThing(dynamic thing, float offset, IReadOnlyList<float> totals, int nbi, decimal newValue)
        {
            switch (nbi)
            {
                case 0:
                case 1:
                case 2:
                    TranslateThing(thing, offset / 55, nbi);
                    break;
                case 3:
                case 4:
                case 5:
                    ScaleThing(thing, offset, totals, nbi, uniformScaleCheckbox.Checked, false);
                    if (uniformScaleCheckbox.Checked && selectedMeshIndices.Count != 0) scaleXNumBox.Value = scaleYNumBox.Value = scaleZNumBox.Value = newValue;
                    break;
                case 6:
                case 7:
                case 8:
                    RotateThing(thing, offset, totals, nbi);
                    break;
            }
        }

        private static float[] CalculateMeshTotals()
        {
            float vertexCount = 0, xSum = 0, ySum = 0, zSum = 0;
            foreach (int i in selectedMeshIndices)
            {
                foreach (FLVER.Vertex v in flver.Meshes[i].Vertices)
                {
                    xSum += v.Positions[0].X;
                    ySum += v.Positions[0].Y;
                    zSum += v.Positions[0].Z;
                }
                vertexCount += flver.Meshes[i].Vertices.Count;
            }
            return new[] { xSum / vertexCount, ySum / vertexCount, zSum / vertexCount };
        }

        private void ModifierNumBoxValueChanged(object sender, EventArgs e)
        {
            if (isSettingDefaultInfo) return;
            var numBox = (NumericUpDown)sender;
            int nbi = meshModifiersNumBoxesContainer.GetRow(numBox) * meshModifiersNumBoxesContainer.ColumnCount
                + meshModifiersNumBoxesContainer.GetColumn(numBox);
            var newNumVal = (float)(numBox == rotXNumBox || numBox == rotYNumBox || numBox == rotZNumBox ? ToRadians(numBox.Value) : numBox.Value);
            if (numBox == rotYNumBox && selectedMeshIndices.Count != 0) newNumVal = -newNumVal;
            if (numBox == scaleXNumBox || numBox == scaleYNumBox || numBox == scaleZNumBox) newNumVal = (float)(numBox.Value / 300);
            float offset = newNumVal < prevNumVal ?
                -Math.Abs(newNumVal - prevNumVal)
                : Math.Abs(newNumVal - prevNumVal);
            float[] totals = CalculateMeshTotals();
            foreach (FLVER.Vertex v in selectedMeshIndices.SelectMany(i => flver.Meshes[i].Vertices))
                TransformThing(v, offset, totals, nbi, numBox.Value);
            foreach (int i in selectedDummyIndices)
                TransformThing(flver.Dummies[i], offset, totals, nbi, numBox.Value);
            UpdateMesh();
            prevNumVal = newNumVal;
        }

        private void ModifierNumBoxFocused(object sender, EventArgs e)
        {
            var numBox = (NumericUpDown)sender;
            prevNumVal = (float)(numBox == rotXNumBox || numBox == rotYNumBox || numBox == rotZNumBox ? ToRadians(numBox.Value) : numBox.Value);
            if (numBox == rotYNumBox && selectedMeshIndices.Count != 0) prevNumVal = -prevNumVal;
            if (numBox == scaleXNumBox || numBox == scaleYNumBox || numBox == scaleZNumBox) prevNumVal = (float)(numBox.Value / 300);
        }

        private void MaterialsTableOkButtonClicked(object sender, MouseEventArgs e)
        {
            try
            {
                foreach (DataGridViewRow row in materialsTable.Rows)
                {
                    if (!(bool)row.Cells[mtApplyPresetCbIndex].Value) continue;
                    string prevName = flver.Materials[row.Index].Name;
                    flver.Materials[row.Index] = new JavaScriptSerializer().Deserialize<FLVER.Material>(
                        new JavaScriptSerializer().Serialize(materialPresets.Values.ToArray()[materialPresetsSelector.SelectedIndex]));
                    flver.Materials[row.Index].Name = prevName;
                }
                for (int i = flver.Materials.Count - 1; i >= 0; --i)
                {
                    if (!(bool)materialsTable.Rows[i].Cells[mtDeleteCbIndex].Value || flver.Materials.Count <= 1) continue;
                    flver.Materials.RemoveAt(i);
                    foreach (FLVER.Mesh mesh in flver.Meshes.Where(mesh => mesh.MaterialIndex > 0))
                        mesh.MaterialIndex--;
                }
                ClearViewerMaterialHighlight();
                UpdateUI();
                UpdateMesh();
                viewer.RefreshTextures();
            }
            catch { }
        }

        private void SelectAllThings(DataGridView table, int columnIndex)
        {
            bool allChecked = AreCheckboxesInDataTableAllChecked(table, columnIndex);
            foreach (DataGridViewRow row in table.Rows)
            {
                if ((bool)row.Cells[columnIndex].Value && !allChecked) continue;
                switch (columnIndex)
                {
                    case 4:
                        UpdateSelectedDummyIndices(columnIndex, row.Index);
                        break;
                    case 3:
                        UpdateSelectedMeshIndices(columnIndex, row.Index);
                        break;
                }
            }
            switch (columnIndex)
            {
                case 4:
                    UpdateSelectedDummies();
                    break;
                case 3:
                    UpdateSelectedMeshes();
                    break;
            }
            isSettingDefaultInfo = true;
            ToggleCheckboxesInDataTable(table, columnIndex);
            isSettingDefaultInfo = false;
        }

        private void SelectAllMeshesButtonClicked(object sender, MouseEventArgs e)
        {
            SelectAllThings(meshTable, 3);
        }

        private void SelectAllDummiesButtonClicked(object sender, MouseEventArgs e)
        {
            SelectAllThings(dummiesTable, 4);
        }

        private void DeleteSelectedButtonClicked(object sender, MouseEventArgs e)
        {
            for (int i = flver.Meshes.Count - 1; i >= 0; --i)
            {
                if (!(bool)meshTable.Rows[i].Cells[3].Value) continue;
                if (deleteFacesetsCheckbox.Checked)
                {
                    foreach (FLVER.FaceSet fs in flver.Meshes[i].FaceSets)
                        for (var j = 0; j < fs.Vertices.Length; ++j)
                            fs.Vertices[j] = 1;
                }
                else
                {
                    selectedMeshIndices.RemoveAt(selectedMeshIndices.IndexOf(i));
                    flver.Meshes.RemoveAt(i);
                }
            }
            for (int i = flver.Dummies.Count - 1; i >= 0; --i)
            {
                if (!(bool)dummiesTable.Rows[i].Cells[4].Value) continue;
                selectedDummyIndices.RemoveAt(selectedDummyIndices.IndexOf(i));
                flver.Dummies.RemoveAt(i);
            }
            ResetModifierNumBoxValues();
            meshModifiersContainer.Enabled = meshIsSelected = dummyIsSelected = false;
            DeselectAllSelectedThings();
            UpdateUI();
            UpdateMesh();
        }

        private void ModifierNumBoxEnterPressed(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) e.SuppressKeyPress = true;
        }

        private static void SaveFLVERFile(string filePath)
        {
            if (filePath.EndsWith(".flver") || filePath.EndsWith(".flver.bak"))
            {
                BackupFLVERFile(filePath);
                flver.Write(filePath);
            }
            else if (filePath.EndsWith(".dcx"))
            {
                BackupFLVERFile(filePath);
                flver.Write(filePath);
                flverBnd.Files[currFlverFileBinderIndex].Bytes = File.ReadAllBytes(filePath);
                flverBnd.Write(filePath, flverArchiveType);
            }
        }

        private static void SaveFLVERAs()
        {
            string bndFilter = flverFilePath.EndsWith(".dcx") ? "|BND File (*.dcx)|*.dcx" : "";
            var dialog = new SaveFileDialog
                { Filter = $@"FLVER File (*.flver)|*.flver{bndFilter}", FileName = Path.GetFileNameWithoutExtension(flverFilePath.Replace(".dcx", "")) };
            if (dialog.ShowDialog() != DialogResult.OK) return;
            SaveFLVERFile(dialog.FileName);
        }

        private void SaveButtonClicked(object sender, EventArgs e)
        {
            SaveFLVERFile(flverFilePath);
        }

        private void SaveAsButtonClicked(object sender, EventArgs e)
        {
            SaveFLVERAs();
        }

        private static bool IsTextBoxCell(object sender, int columnIndex, int rowIndex)
        {
            return ((DataGridView)sender)[columnIndex, rowIndex] is DataGridViewTextBoxCell;
        }

        private void BonesTableCellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (isSettingDefaultInfo || !IsTextBoxCell(sender, e.ColumnIndex, e.RowIndex)) return;
            try
            {
                var bonesTableValue = bonesTable[e.ColumnIndex, e.RowIndex].Value?.ToString();
                if (bonesTableValue != null)
                {
                    switch (e.ColumnIndex)
                    {
                        case 1:
                            flver.Bones[e.RowIndex].Name = bonesTableValue;
                            break;
                        case 2:
                            flver.Bones[e.RowIndex].ParentIndex = short.Parse(bonesTableValue);
                            break;
                        case 3:
                            flver.Bones[e.RowIndex].ChildIndex = short.Parse(bonesTableValue);
                            break;
                        case 4:
                        case 5:
                        case 6:
                        case 7:
                        case 8:
                            string[] comp = bonesTableValue.Split(',');
                            var vector = new System.Numerics.Vector3(float.Parse(comp[0]), float.Parse(comp[1]), float.Parse(comp[2]));
                            flver.Bones[e.RowIndex].Translation = e.ColumnIndex == 4 ? vector : flver.Bones[e.RowIndex].Translation;
                            flver.Bones[e.RowIndex].Scale = e.ColumnIndex == 5 ? vector : flver.Bones[e.RowIndex].Scale;
                            flver.Bones[e.RowIndex].Rotation = e.ColumnIndex == 6 ? vector : flver.Bones[e.RowIndex].Rotation;
                            flver.Bones[e.RowIndex].BoundingBoxMin = e.ColumnIndex == 7 ? vector : flver.Bones[e.RowIndex].BoundingBoxMin;
                            flver.Bones[e.RowIndex].BoundingBoxMax = e.ColumnIndex == 8 ? vector : flver.Bones[e.RowIndex].BoundingBoxMax;
                            break;
                    }
                }
            }
            catch { }
            UpdateUI();
            UpdateMesh();
        }

        private void MaterialsTableCellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (isSettingDefaultInfo || !IsTextBoxCell(sender, e.ColumnIndex, e.RowIndex)) return;
            try
            {
                var materialsTableValue = materialsTable[e.ColumnIndex, e.RowIndex].Value?.ToString();
                if (materialsTableValue != null)
                {
                    switch (e.ColumnIndex)
                    {
                        case 1:
                            flver.Materials[e.RowIndex].Name = materialsTableValue;
                            break;
                        case 2:
                            flver.Materials[e.RowIndex].Flags = int.Parse(materialsTableValue);
                            break;
                        case 3:
                            flver.Materials[e.RowIndex].MTD = materialsTableValue;
                            break;
                        case 4:
                            flver.Materials[e.RowIndex].Unk18 = int.Parse(materialsTableValue);
                            break;
                    }
                }
            }
            catch { }
            UpdateUI();
            UpdateMesh();
            viewer.RefreshTextures();
        }

        private void TexturesTableCellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (isSettingDefaultInfo || !IsTextBoxCell(sender, e.ColumnIndex, e.RowIndex)) return;
            try
            {
                var textureTableValue = texturesTable[e.ColumnIndex, e.RowIndex].Value?.ToString();
                if (textureTableValue != null)
                {
                    switch (e.ColumnIndex)
                    {
                        case 0:
                            flver.Materials[selectedMaterialIndex].Textures[e.RowIndex].Type = textureTableValue;
                            break;
                        case 1:
                            flver.Materials[selectedMaterialIndex].Textures[e.RowIndex].Path = textureTableValue;
                            break;
                    }
                }
            }
            catch { }
            UpdateMesh();
            viewer.RefreshTextures();
        }

        public static void ShowInformationDialog(string str)
        {
            MessageBox.Show(str, @"Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static void ShowErrorDialog(string str)
        {
            MessageBox.Show(str, @"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public static DialogResult ShowQuestionDialog(string str)
        {
            return MessageBox.Show(str, @"Question", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        }

        private void MeshTableCellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (isSettingDefaultInfo || !IsTextBoxCell(sender, e.ColumnIndex, e.RowIndex) || e.ColumnIndex != 2) return;
            try
            {
                var boneWeightValue = meshTable[2, e.RowIndex].Value?.ToString();
                if (boneWeightValue != null)
                {
                    int newBoneWeight = int.Parse(boneWeightValue);
                    foreach (FLVER.Vertex v in flver.Meshes[e.RowIndex].Vertices)
                    {
                        for (var i = 0; i < v.Positions.Count; ++i)
                        {
                            if (v.BoneWeights == null)
                            {
                                v.BoneWeights = new float[4];
                                v.BoneIndices = new int[4];
                            }
                            for (var j = 0; j < v.BoneWeights.Length; ++j)
                                v.BoneWeights[j] = 0;
                            v.BoneIndices[0] = newBoneWeight;
                            v.BoneWeights[0] = 1;
                        }
                    }
                    if (!flver.Meshes[e.RowIndex].BoneIndices.Contains(newBoneWeight)) flver.Meshes[e.RowIndex].BoneIndices.Add(newBoneWeight);
                    flver.Meshes[e.RowIndex].Dynamic = true;
                }
            }
            catch { }
            UpdateUI();
            UpdateMesh();
        }

        private void ReverseFaceSetsCheckboxChanged(object sender, EventArgs e)
        {
            if (isSettingDefaultInfo) return;
            foreach (FLVER.FaceSet fs in selectedMeshIndices.SelectMany(i => flver.Meshes[i].FaceSets))
            {
                for (var j = 0; j < fs.Vertices.Length; j += 3)
                    (fs.Vertices[j + 1], fs.Vertices[j + 2]) = (fs.Vertices[j + 2], fs.Vertices[j + 1]);
            }
            UpdateMesh();
        }

        private void ReverseNormalsCheckboxChanged(object sender, EventArgs e)
        {
            if (isSettingDefaultInfo) return;
            foreach (FLVER.Vertex v in selectedMeshIndices.SelectMany(i => flver.Meshes[i].Vertices))
            {
                for (var j = 0; j < v.Normals.Count; ++j)
                    v.Normals[j] = new Vector4(-v.Normals[j].X, -v.Normals[j].Y, -v.Normals[j].Z, v.Normals[j].W);
                for (var j = 0; j < v.Tangents.Count; ++j)
                    v.Tangents[j] = new Vector4(-v.Tangents[j].X, -v.Tangents[j].Y, -v.Tangents[j].Z, v.Tangents[j].W);
            }
            ShowInformationDialog("Mesh normals have been reversed!");
            UpdateMesh();
        }

        private void ToggleBackFacesCheckboxChanged(object sender, EventArgs e)
        {
            if (isSettingDefaultInfo) return;
            foreach (FLVER.FaceSet fs in selectedMeshIndices.SelectMany(i => flver.Meshes[i].FaceSets))
                fs.CullBackfaces = !fs.CullBackfaces;
            ShowInformationDialog("Mesh backfaces have been toggled!");
        }

        private static void UpdateHeaderBoundingBox(FLVER.FLVERHeader header, System.Numerics.Vector3 vertexPos)
        {
            float minX = Math.Min(header.BoundingBoxMin.X, vertexPos.X);
            float minY = Math.Min(header.BoundingBoxMin.Y, vertexPos.Y);
            float minZ = Math.Min(header.BoundingBoxMin.Z, vertexPos.Z);
            float maxX = Math.Max(header.BoundingBoxMax.X, vertexPos.X);
            float maxY = Math.Max(header.BoundingBoxMax.Y, vertexPos.Y);
            float maxZ = Math.Max(header.BoundingBoxMax.Z, vertexPos.Z);
            header.BoundingBoxMin = new System.Numerics.Vector3(minX, minY, minZ);
            header.BoundingBoxMax = new System.Numerics.Vector3(maxX, maxY, maxZ);
        }

        private static void UpdateMeshBoundingBox(FLVER.Mesh mesh, System.Numerics.Vector3 vertexPos)
        {
            float minX = Math.Min(mesh.BoundingBoxMin.X, vertexPos.X);
            float minY = Math.Min(mesh.BoundingBoxMin.Y, vertexPos.Y);
            float minZ = Math.Min(mesh.BoundingBoxMin.Z, vertexPos.Z);
            float maxX = Math.Max(mesh.BoundingBoxMax.X, vertexPos.X);
            float maxY = Math.Max(mesh.BoundingBoxMax.Y, vertexPos.Y);
            float maxZ = Math.Max(mesh.BoundingBoxMax.Z, vertexPos.Z);
            mesh.BoundingBoxMin = new System.Numerics.Vector3(minX, minY, minZ);
            mesh.BoundingBoxMax = new System.Numerics.Vector3(maxX, maxY, maxZ);
        }

        private static Matrix4x4 GetNMatrix(FLVER.Bone b)
        {
            return Matrix4x4.CreateScale(b.Scale)
                * Matrix4x4.CreateRotationX(b.Rotation.X)
                * Matrix4x4.CreateRotationZ(b.Rotation.Z)
                * Matrix4x4.CreateRotationY(b.Rotation.Y)
                * Matrix4x4.CreateTranslation(b.Translation);
        }

        private static FLVER.Bone GetParent(FLVER.Bone b, IReadOnlyList<FLVER.Bone> bones)
        {
            if (b.ParentIndex >= 0 && b.ParentIndex < bones.Count) return bones[b.ParentIndex];
            return null;
        }

        private static Matrix4x4 GetAbsoluteNMatrix(FLVER.Bone b, IReadOnlyList<FLVER.Bone> bones)
        {
            Matrix4x4 result = Matrix4x4.Identity;
            FLVER.Bone parentBone = b;
            while (parentBone != null)
            {
                Matrix4x4 m = GetNMatrix(parentBone);
                result *= m;
                parentBone = GetParent(parentBone, bones);
            }
            return result;
        }

        private static void UpdateBonesBoundingBox(FLVER.Bone b, IReadOnlyList<FLVER.Bone> bones, System.Numerics.Vector3 vertexPos)
        {
            Matrix4x4 boneAbsoluteMatrix = GetAbsoluteNMatrix(b, bones);
            if (!Matrix4x4.Invert(boneAbsoluteMatrix, out Matrix4x4 invertedBoneMatrix)) return;
            System.Numerics.Vector3 posForBBox = System.Numerics.Vector3.Transform(vertexPos, invertedBoneMatrix);
            float minX = Math.Min(b.BoundingBoxMin.X, posForBBox.X);
            float minY = Math.Min(b.BoundingBoxMin.Y, posForBBox.Y);
            float minZ = Math.Min(b.BoundingBoxMin.Z, posForBBox.Z);
            float maxX = Math.Max(b.BoundingBoxMax.X, posForBBox.X);
            float maxY = Math.Max(b.BoundingBoxMax.Y, posForBBox.Y);
            float maxZ = Math.Max(b.BoundingBoxMax.Z, posForBBox.Z);
            b.BoundingBoxMin = new System.Numerics.Vector3(minX, minY, minZ);
            b.BoundingBoxMax = new System.Numerics.Vector3(maxX, maxY, maxZ);
        }

        private void SolveAllBBsButtonClicked(object sender, MouseEventArgs e)
        {
            flver.Header.BoundingBoxMin = new System.Numerics.Vector3();
            flver.Header.BoundingBoxMax = new System.Numerics.Vector3();
            foreach (FLVER.Bone bone in flver.Bones)
            {
                bone.BoundingBoxMin = new System.Numerics.Vector3();
                bone.BoundingBoxMax = new System.Numerics.Vector3();
            }
            foreach (FLVER.Mesh mesh in flver.Meshes)
            {
                foreach (FLVER.Vertex vertex in mesh.Vertices)
                {
                    UpdateHeaderBoundingBox(flver.Header, vertex.Positions[0]);
                    UpdateMeshBoundingBox(mesh, vertex.Positions[0]);
                    if (vertex.BoneIndices == null) continue;
                    foreach (int boneIndex in vertex.BoneIndices)
                    {
                        var boneDoesNotExist = false;
                        if (boneIndex >= 0 && boneIndex < flver.Bones.Count) flver.Bones[boneIndex].Unk3C = 0;
                        else boneDoesNotExist = true;
                        if (!boneDoesNotExist) UpdateBonesBoundingBox(flver.Bones[boneIndex], flver.Bones, vertex.Positions[0]);
                    }
                }
            }
            ShowInformationDialog("Solved all bone and mesh bounding boxes!");
            UpdateMesh();
        }

        private void DummiesTableOKButtonClicked(object sender, MouseEventArgs e)
        {
            if (dummyPresetsSelector.SelectedIndex < 0) return;
            DeselectAllSelectedThings();
            string dummyJson = new JavaScriptSerializer().Serialize(dummyPresets.Values.ToArray()[dummyPresetsSelector.SelectedIndex]);
            flver.Dummies = new JavaScriptSerializer().Deserialize<List<FLVER.Dummy>>(dummyJson);
            UpdateUI();
            UpdateMesh();
        }

        private void AddAllDummiesToPresetsButtonClicked(object sender, MouseEventArgs e)
        {
            var key = flver.Dummies.Count.ToString();
            if (dummyPresets.ContainsKey(key)) return;
            dummyPresets.Add(key, new JavaScriptSerializer().Deserialize<object>(new JavaScriptSerializer().Serialize(flver.Dummies)));
            File.WriteAllText(dummyPresetsFilePath, new JavaScriptSerializer().Serialize(dummyPresets));
            LoadDummyPresets();
        }

        private static void ExportFLVERAsDAE()
        {
            var dialog = new SaveFileDialog { FileName = $"{Path.GetFileNameWithoutExtension(flverFilePath)}.dae", Filter = @"Collada DAE File (*.dae)|*.dae" };
            if (dialog.ShowDialog() != DialogResult.OK) return;
            try
            {
                var scene = new Scene { RootNode = new Node() };
                foreach (FLVER.Material m in flver.Materials)
                    scene.Materials.Add(new Material { Name = m.Name });
                for (var i = 0; i < flver.Meshes.Count; ++i)
                {
                    FLVER.Mesh m = flver.Meshes[i];
                    var newMesh = new Mesh("Mesh_M" + i, PrimitiveType.Triangle);
                    foreach (FLVER.Vertex v in m.Vertices)
                    {
                        newMesh.Vertices.Add(new Assimp.Vector3D(v.Positions[0].X, v.Positions[0].Y, v.Positions[0].Z));
                        newMesh.Normals.Add(new Assimp.Vector3D(v.Normals[0].X, v.Normals[0].Y, v.Normals[0].Z));
                        newMesh.Tangents.Add(new Assimp.Vector3D(v.Tangents[0].X, v.Tangents[0].Y, v.Tangents[0].Z));
                        for (var j = 0; j < v.UVs.Count; ++j)
                            newMesh.TextureCoordinateChannels[j].Add(new Assimp.Vector3D(v.UVs[j].X, 1 - v.UVs[j].Y, 0));
                    }
                    foreach (uint[] arr in m.FaceSets.SelectMany(fs => fs.GetFaces()))
                        newMesh.Faces.Add(new Face(new[] { (int)arr[0], (int)arr[1], (int)arr[2] }));
                    newMesh.MaterialIndex = m.MaterialIndex;
                    scene.Meshes.Add(newMesh);
                    var nodeBase = new Node { Name = "M_" + i + "_" + flver.Materials[m.MaterialIndex].Name };
                    nodeBase.MeshIndices.Add(i);
                    scene.RootNode.Children.Add(nodeBase);
                }
                var exporter = new AssimpContext();
                bool hasExported = exporter.ExportFile(scene, dialog.FileName, "collada");
                if (hasExported) ShowInformationDialog("Successfully exported FLVER file to the Collada DAE format!");
            }
            catch
            {
                ShowInformationDialog("An error occurred during the exporting process.");
            }
        }

        private void ExportToolStripMenuItemClicked(object sender, EventArgs e)
        {
            ExportFLVERAsDAE();
        }

        private void ImportFLVERFile()
        {
            if (!Program.ImportFBX()) return;
            flver = Program.flver;
            DeselectAllSelectedThings();
            UpdateUI();
            UpdateMesh();
            viewer.RefreshTextures();
        }

        private void ImportToolStripMenuItemClicked(object sender, EventArgs e)
        {
            ImportFLVERFile();
        }

        private void MergeFLVERFile()
        {
            string newFlverFilePath = PromptFLVERModel();
            if (newFlverFilePath == "") return;
            try
            {
                FLVER newFlver = IsFLVERPath(newFlverFilePath) ? FLVER.Read(newFlverFilePath) :
                    ReadFLVERFromDCXPath(newFlverFilePath, false, false, false);
                int materialOffset = flver.Materials.Count;
                int layoutOffset = flver.BufferLayouts.Count;
                var newFlverToCurrentFlver = new Dictionary<int, int>();
                for (var i = 0; i < newFlver.Bones.Count; ++i)
                {
                    FLVER.Bone attachBone = newFlver.Bones[i];
                    for (var j = 0; j < flver.Bones.Count; ++j)
                    {
                        if (attachBone.Name != flver.Bones[j].Name) continue;
                        newFlverToCurrentFlver.Add(i, j);
                        break;
                    }
                }
                foreach (FLVER.Mesh m in newFlver.Meshes)
                {
                    m.MaterialIndex += materialOffset;
                    foreach (FLVER.VertexBuffer vb in m.VertexBuffers)
                        vb.LayoutIndex += layoutOffset;
                    foreach (FLVER.Vertex v in m.Vertices.Where(v => v.BoneIndices != null))
                    {
                        for (var i = 0; i < v.BoneIndices.Length; ++i)
                        {
                            if (newFlverToCurrentFlver.ContainsKey(v.BoneIndices[i])) v.BoneIndices[i] = newFlverToCurrentFlver[v.BoneIndices[i]];
                        }
                    }
                }
                flver.BufferLayouts = flver.BufferLayouts.Concat(newFlver.BufferLayouts).ToList();
                flver.Meshes = flver.Meshes.Concat(newFlver.Meshes).ToList();
                flver.Materials = flver.Materials.Concat(newFlver.Materials).ToList();
                ShowInformationDialog(@"Successfully attached new FLVER to the current one!");
                DeselectAllSelectedThings();
                UpdateUI();
                UpdateMesh();
                viewer.RefreshTextures();
            }
            catch
            {
                ShowErrorDialog("An error occurred while attempting to merge external FLVER with the current one.");
            }
        }

        private void MergeToolStripMenuItemClicked(object sender, EventArgs e)
        {
            MergeFLVERFile();
        }

        private void MainWindowClosing(object sender, CancelEventArgs e)
        {
            DialogResult result = MessageBox.Show(@"Do you want to save changes to the FLVER before quitting?", @"Warning", MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Exclamation);
            if (result != DialogResult.Yes)
            {
                if (result == DialogResult.Cancel) e.Cancel = true;
            }
            else SaveFLVERFile(flverFilePath);
        }

        private void AddDummyButtonClicked(object sender, MouseEventArgs e)
        {
            var newDummy = new FLVER.Dummy
            {
                Position = flver.Dummies.Count > 0 ? flver.Dummies[flver.Dummies.Count - 1].Position : new System.Numerics.Vector3(0, 0, 0),
                ReferenceID = -1
            };
            flver.Dummies.Add(newDummy);
            DeselectAllSelectedThings();
            UpdateUI();
            UpdateMesh();
        }

        private void DummiesTableCellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (isSettingDefaultInfo || !IsTextBoxCell(sender, e.ColumnIndex, e.RowIndex)) return;
            try
            {
                var dummiesTableValue = dummiesTable[e.ColumnIndex, e.RowIndex].Value?.ToString();
                if (dummiesTableValue != null)
                {
                    short parsed = short.Parse(dummiesTableValue);
                    switch (e.ColumnIndex)
                    {
                        case 1:
                            flver.Dummies[e.RowIndex].ReferenceID = parsed;
                            break;
                        case 2:
                            flver.Dummies[e.RowIndex].AttachBoneIndex = parsed;
                            break;
                        case 3:
                            flver.Dummies[e.RowIndex].DummyBoneIndex = parsed;
                            break;
                    }
                }
            }
            catch { }
            UpdateUI();
            UpdateMesh();
        }

        private void CenterToWorldButtonClicked(object sender, MouseEventArgs e)
        {
            float[] totals = CalculateMeshTotals();
            foreach (FLVER.Vertex v in selectedMeshIndices.SelectMany(i => flver.Meshes[i].Vertices))
                v.Positions[0] = new System.Numerics.Vector3(v.Positions[0].X - totals[0], v.Positions[0].Y - totals[1], v.Positions[0].Z - totals[2]);
            foreach (FLVER.Dummy d in selectedDummyIndices.Select(i => flver.Dummies[i]))
                d.Position = new System.Numerics.Vector3(d.Position.X - totals[0], d.Position.Y - totals[1], d.Position.Z - totals[2]);
            UpdateMesh();
        }

        private static decimal ToRadians(decimal degrees) { return degrees * (decimal)(Math.PI / 180); }

        private void LoadJSON(int type)
        {
            var dialog = new OpenFileDialog { Filter = @"JSON File (*.json)|*.json" };
            if (dialog.ShowDialog() != DialogResult.OK) return;
            try
            {
                string jsonText = File.ReadAllText(dialog.FileName);
                switch (type)
                {
                    case 0:
                        flver.Bones = new JavaScriptSerializer().Deserialize<List<FLVER.Bone>>(jsonText);
                        break;
                    case 1:
                        flver.Materials = new JavaScriptSerializer().Deserialize<List<FLVER.Material>>(jsonText);
                        break;
                }
                DeselectAllSelectedThings();
                ClearViewerMaterialHighlight();
                UpdateUI();
                UpdateMesh();
                ShowInformationDialog("Successfully parsed JSON!");
            }
            catch
            {
                ShowInformationDialog("The JSON could not be found or read.");
            }
        }

        private static void ExportJSON(dynamic list)
        {
            var dialog = new SaveFileDialog { Filter = @"JSON File (*.json)|*.json" };
            if (dialog.ShowDialog() != DialogResult.OK) return;
            try
            {
                File.WriteAllText(dialog.FileName, new JavaScriptSerializer().Serialize(list));
                ShowInformationDialog("Successfully exported JSON!");
            }
            catch
            {
                ShowInformationDialog("An error occurred while attempting to export JSON.");
            }
        }

        private void LoadBonesJSONButtonClicked(object sender, EventArgs e)
        {
            LoadJSON(0);
        }

        private void LoadMaterialsJSONButtonClicked(object sender, EventArgs e)
        {
            LoadJSON(1);
        }

        private void ExportBonesJSONButtonClicked(object sender, EventArgs e)
        {
            ExportJSON(flver.Bones);
        }

        private void ExportMaterialsJSONButtonClicked(object sender, EventArgs e)
        {
            ExportJSON(flver.Materials);
        }

        private void BrowsePresetsFile(string configPath, bool materialPresetsFile)
        {
            var dialog = new OpenFileDialog { Filter = @"JSON File (*.json)|*.json", Multiselect = false };
            if (dialog.ShowDialog() != DialogResult.OK) return;
            File.WriteAllText(configPath, dialog.FileName);
            if (materialPresetsFile) LoadMaterialPresets();
            else LoadDummyPresets();
            ShowInformationDialog("Successfully loaded presets file!");
        }

        private void BrowseMaterialPresetsFileButtonClicked(object sender, EventArgs e)
        {
            BrowsePresetsFile(mPresetsConfigFilePath, true);
        }

        private void BrowseDummyPresetsFileButtonClicked(object sender, EventArgs e)
        {
            BrowsePresetsFile(dPresetsConfigFilePath, false);
        }

        private void ToggleTextureRefreshButtonClicked(object sender, EventArgs e)
        {
            if (textureRefreshEnabled)
            {
                ShowInformationDialog("Texture refreshing is now disabled to help improve performance!");
            }
            else
            {
                ShowInformationDialog("Texture refreshing is now enabled!");
                viewer.RefreshTextures();
            }
            textureRefreshEnabled = !textureRefreshEnabled;
        }

        private void MergePresets(bool materialPresetsFile)
        {
            var dialog = new OpenFileDialog { Filter = @"JSON File (*.json)|*.json", Multiselect = false };
            if (dialog.ShowDialog() != DialogResult.OK) return;
            var newPresets = new JavaScriptSerializer().Deserialize<Dictionary<object, object>>(File.ReadAllText(dialog.FileName));
            Dictionary<object, object> presets = materialPresetsFile ? materialPresets : dummyPresets;
            foreach (KeyValuePair<object, object> preset in newPresets.Where(preset => !presets.ContainsKey(preset.Key)))
                presets.Add(preset.Key, preset.Value);
            File.WriteAllText(materialPresetsFile ? materialPresetsFilePath : dummyPresetsFilePath, new JavaScriptSerializer().Serialize(presets));
            if (materialPresetsFile) LoadMaterialPresets();
            else LoadDummyPresets();
            ShowInformationDialog("Successfully merged presets with current presets!");
        }

        private void MergeMaterialPresetsButtonClicked(object sender, EventArgs e)
        {
            MergePresets(true);
        }

        private void MergeDummyPresetsButtonClicked(object sender, EventArgs e)
        {
            MergePresets(false);
        }

        private void SetAllBBsMaxSizeButtonClicked(object sender, EventArgs e)
        {
            var minVector = new System.Numerics.Vector3(0, 0, 0);
            var maxVector = new System.Numerics.Vector3(999, 999, 999);
            flver.Header.BoundingBoxMin = maxVector;
            flver.Header.BoundingBoxMax = minVector;
            foreach (FLVER.Mesh mesh in from mesh in flver.Meshes from vertex in mesh.Vertices select mesh)
            {
                mesh.BoundingBoxMin = maxVector;
                mesh.BoundingBoxMax = minVector;
            }
            ShowInformationDialog("Set all mesh bounding boxes to maximum size!");
            UpdateMesh();
        }

        private void TabWindowDrawItem(object sender, DrawItemEventArgs e)
        {
            Rectangle rec = tabWindow.ClientRectangle;
            var StrFormat = new StringFormat();
            StrFormat.LineAlignment = StringAlignment.Center;
            StrFormat.Alignment = StringAlignment.Center;
            var backColor = new SolidBrush(tabWindowBackColor);
            e.Graphics.FillRectangle(backColor, rec);
            Font fntTab = e.Font;
            for (var i = 0; i < tabWindow.TabPages.Count; i++)
            {
                RectangleF tabTextArea = tabWindow.GetTabRect(i);
                var fontColor = new SolidBrush(tabWindowForeColor);
                e.Graphics.DrawString(tabWindow.TabPages[i].Text, fntTab, fontColor, tabTextArea, StrFormat);
            }
        }

        private void MainWindowLoad(object sender, EventArgs e)
        {
            UpdateWindowTitle();
            tabWindow.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabWindow.DrawItem += TabWindowDrawItem;
        }

        private void ApplyMATBINTexturesButtonClicked(object sender, EventArgs e)
        {
            if (matBinBndPath == null)
            {
                try
                {
                    matBinBndPath = File.ReadAllText(matBinBndConfigPath);
                    File.ReadAllBytes(matBinBndPath);
                }
                catch
                {
                    matBinBndPath = null;
                    var dialog = new OpenFileDialog { Filter = @"MATBIN BND (*.matbinbnd.dcx)|*.matbinbnd.dcx" };
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        matBinBndPath = dialog.FileName;
                        File.WriteAllText(matBinBndConfigPath, matBinBndPath);
                    }
                    else
                    {
                        return;
                    }
                }
            }
            if (matBinBnd == null) matBinBnd = BND4.Read(matBinBndPath);
            foreach (BinderFile matBinFile in matBinBnd.Files)
            {
                string rawMaterialFileName = Path.GetFileNameWithoutExtension(flver.Materials[selectedMaterialIndex].MTD);
                string rawMatBinFileName = Path.GetFileNameWithoutExtension(matBinFile.Name);
                if (rawMaterialFileName != rawMatBinFileName) continue;
                var matBin = new MATBIN();
                matBin.Read(new BinaryReaderEx(false, matBinFile.Bytes));
                if (matBin.Samplers.Any(sampler => sampler.Path != ""))
                {
                    flver.Materials[selectedMaterialIndex].Textures.Clear();
                    foreach (FLVER.Texture newTexture in matBin.Samplers.Select(sampler => new FLVER.Texture { Type = sampler.Type, Path = sampler.Path }))
                        flver.Materials[selectedMaterialIndex].Textures.Add(newTexture);
                }
                break;
            }
            UpdateTexturesTable();
        }

        private void DummyThicknessSelectorSelectedIndexChanged(object sender, EventArgs e)
        {
            if (isSettingDefaultInfo) return;
            dummyThickness = int.Parse(dummyThicknessSelector.Items[dummyThicknessSelector.SelectedIndex].ToString());
            File.WriteAllText(dummyThicknessConfigPath, dummyThickness.ToString());
            UpdateMesh();
        }

        private void MaterialsPagePanelsContainerSplitterMoved(object sender, SplitterEventArgs e)
        {
            File.WriteAllText(matTableViewConfigPath, e.SplitX.ToString());
        }

        private static void MirrorMesh(int nbi)
        {
            foreach (FLVER.Vertex v in selectedMeshIndices.SelectMany(i => flver.Meshes[i].Vertices))
                ScaleThing(v, 0, new float[3], nbi, false, true);
            foreach (FLVER.Dummy d in selectedDummyIndices.Select(i => flver.Dummies[i]))
                ScaleThing(d, 0, new float[3], nbi, false, true);
            UpdateMesh();
        }

        private void MirrorXCheckboxCheckedChanged(object sender, EventArgs e)
        {
            MirrorMesh(0);
        }

        private void MirrorYCheckboxCheckedChanged(object sender, EventArgs e)
        {
            MirrorMesh(1);
        }

        private void MirrorZCheckboxCheckedChanged(object sender, EventArgs e)
        {
            MirrorMesh(2);
        }

        private void ToggleAutoSaveToolStripMenuItemClick(object sender, EventArgs e)
        {
            ToggleAutoSaveState(true, true);
        }

        private void AutoSaveTimerTick(object sender, EventArgs e)
        {
            if (flverFilePath != null) SaveFLVERFile(flverFilePath);
        }

        private bool CheckAutoSaveInterval(string intervalStr)
        {
            if (!Regex.IsMatch(intervalStr, "^[0-9]*$")) return false;
            try
            {
                int newInterval = int.Parse(intervalStr);
                if (newInterval == 0 || newInterval > 60) return false;
                autoSaveTimer.Interval = newInterval * 60000;
                File.WriteAllText(autoSaveIntervalConfigPath, intervalStr);
            }
            catch
            {
                return false;
            }
            return true;
        }

        private void AutoSaveIntervalSelectorTextChanged(object sender, EventArgs e)
        {
            if (isSettingDefaultInfo) return;
            if (!CheckAutoSaveInterval(autoSaveIntervalSelector.Text)) autoSaveIntervalSelector.Text = "";
        }

        private void AutoSaveIntervalSelectorKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            preferencesToolStripMenuItem.HideDropDown();
        }

        private void MainWindowKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Control)
            {
                case true when e.KeyCode == Keys.O:
                    e.SuppressKeyPress = true;
                    OpenFLVERFile();
                    break;
                case true when !e.Shift && e.KeyCode == Keys.S:
                    e.SuppressKeyPress = true;
                    SaveFLVERFile(flverFilePath);
                    break;
                case true when e.Shift && e.KeyCode == Keys.S:
                    e.SuppressKeyPress = true;
                    SaveFLVERAs();
                    break;
                case true when e.KeyCode == Keys.E:
                    e.SuppressKeyPress = true;
                    ExportFLVERAsDAE();
                    break;
                case true when e.KeyCode == Keys.I:
                    e.SuppressKeyPress = true;
                    ImportFLVERFile();
                    break;
                case true when e.KeyCode == Keys.M:
                    e.SuppressKeyPress = true;
                    MergeFLVERFile();
                    break;
            }
        }

        public class MATBIN
        {
            public enum ParamType : uint
            {
                Bool = 0,
                Int = 4,
                Int2 = 5,
                Float = 8,
                Float2 = 9,
                Float3 = 10,
                Float4 = 11,
                Float5 = 12
            }

            public MATBIN()
            {
                ShaderPath = "";
                SourcePath = "";
                Params = new List<Param>();
                Samplers = new List<Sampler>();
            }

            public string ShaderPath { get; set; }

            public string SourcePath { get; set; }

            public uint Key { get; set; }

            public List<Param> Params { get; set; }

            public List<Sampler> Samplers { get; set; }

            public void Read(BinaryReaderEx br)
            {
                br.BigEndian = false;
                br.AssertASCII("MAB\0");
                br.AssertInt32(2);
                ShaderPath = br.GetUTF16(br.ReadInt64());
                SourcePath = br.GetUTF16(br.ReadInt64());
                Key = br.ReadUInt32();
                int paramCount = br.ReadInt32();
                int samplerCount = br.ReadInt32();
                br.ReadBytes(0x14);
                Params = new List<Param>(paramCount);
                for (var i = 0; i < paramCount; i++)
                    Params.Add(new Param(br));
                Samplers = new List<Sampler>(samplerCount);
                for (var i = 0; i < samplerCount; i++)
                    Samplers.Add(new Sampler(br));
            }

            [XmlInclude(typeof(int[]))]
            [XmlInclude(typeof(float[]))]
            public class Param
            {
                internal Param(BinaryReaderEx br)
                {
                    Name = br.GetUTF16(br.ReadInt64());
                    long valueOffset = br.ReadInt64();
                    Key = br.ReadUInt32();
                    Type = br.ReadEnum32<ParamType>();
                    br.ReadBytes(0x10);
                    br.StepIn(valueOffset);
                    {
                        switch (Type)
                        {
                            case ParamType.Bool:
                                Value = br.ReadBoolean();
                                break;
                            case ParamType.Int:
                                Value = br.ReadInt32();
                                break;
                            case ParamType.Int2:
                                Value = br.ReadInt32s(2);
                                break;
                            case ParamType.Float:
                                Value = br.ReadSingle();
                                break;
                            case ParamType.Float2:
                                Value = br.ReadSingles(2);
                                break;
                            case ParamType.Float3:
                                Value = br.ReadSingles(3);
                                break;
                            case ParamType.Float4:
                                Value = br.ReadSingles(4);
                                break;
                            case ParamType.Float5:
                                Value = br.ReadSingles(5);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }
                    br.StepOut();
                }

                public string Name { get; set; }

                public object Value { get; set; }

                public uint Key { get; set; }

                public ParamType Type { get; set; }
            }

            public class Sampler
            {
                internal Sampler(BinaryReaderEx br)
                {
                    Type = br.GetUTF16(br.ReadInt64());
                    Path = br.GetUTF16(br.ReadInt64());
                    Key = br.ReadUInt32();
                    Unk14 = br.ReadVector2();
                    br.ReadBytes(0x14);
                }

                public string Type { get; set; }

                public string Path { get; set; }

                public uint Key { get; set; }

                public Vector2 Unk14 { get; set; }
            }
        }

        private void TabWindowDragEnter(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.All;
        }

        private void TabWindowDragDrop(object sender, DragEventArgs e)
        {
            arguments.Add(((string[])e.Data.GetData(DataFormats.FileDrop))[0]);
            OpenFLVERFile();
        }
    }
}
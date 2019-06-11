using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Xml.Serialization;
using System.IO;
using System.Reflection;
using UnityEditorInternal;

namespace MPipeline
{

    class PointEditr : IEditablePoint
    {
        private bool m_Editing;

        private List<Vector3> m_SourcePositions;
        private List<int> m_Selection = new List<int>();

        private SMPSelection m_SerializedSelectedProbes;

        private readonly LightProbe m_Group;
        private bool m_ShouldRecalculateTetrahedra;
        private Vector3 m_LastPosition = Vector3.zero;
        private Quaternion m_LastRotation = Quaternion.identity;
        private Vector3 m_LastScale = Vector3.one;
        private LightProbeEditor m_Inspector;

        public PointEditr(LightProbe group, LightProbeEditor inspector)
        {
            m_Group = group;
            MarkMeshDirty();
            m_SerializedSelectedProbes = ScriptableObject.CreateInstance<SMPSelection>();
            m_SerializedSelectedProbes.hideFlags = HideFlags.HideAndDontSave;
            m_Inspector = inspector;
        }

        public void SetEditing(bool editing)
        {
            m_Editing = editing;
        }

        public void AddProbe(Vector3 position)
        {
            Undo.RegisterCompleteObjectUndo(new UnityEngine.Object[] { m_Group, m_SerializedSelectedProbes }, "Add Probe");
            m_SourcePositions.Add(position);
            SelectProbe(m_SourcePositions.Count - 1);

            MarkMeshDirty();
        }

        private void SelectProbe(int i)
        {
            if (!m_Selection.Contains(i))
                m_Selection.Add(i);
        }

        public void SelectAllProbes()
        {
            DeselectProbes();

            var count = m_SourcePositions.Count;
            for (var i = 0; i < count; i++)
                m_Selection.Add(i);
        }

        public void DeselectProbes()
        {
            m_Selection.Clear();
            m_SerializedSelectedProbes.m_Selection = m_Selection;
        }

        private IEnumerable<Vector3> SelectedProbePositions()
        {
            return m_Selection.Select(t => m_SourcePositions[t]).ToList();
        }

        public void DuplicateSelectedProbes()
        {
            var selectionCount = m_Selection.Count;
            if (selectionCount == 0) return;

            Undo.RegisterCompleteObjectUndo(new UnityEngine.Object[] { m_Group, m_SerializedSelectedProbes }, "Duplicate Probes");

            foreach (var position in SelectedProbePositions())
            {
                m_SourcePositions.Add(position);
            }

            MarkMeshDirty();
        }

        private void CopySelectedProbes()
        {
            //Convert probes to world position for serialization
            var localPositions = SelectedProbePositions();

            var serializer = new XmlSerializer(typeof(Vector3[]));
            var writer = new StringWriter();

            serializer.Serialize(writer, localPositions.Select(pos => m_Group.transform.TransformPoint(pos)).ToArray());
            writer.Close();
            GUIUtility.systemCopyBuffer = writer.ToString();
        }

        private static bool CanPasteProbes()
        {
            try
            {
                var deserializer = new XmlSerializer(typeof(Vector3[]));
                var reader = new StringReader(GUIUtility.systemCopyBuffer);
                deserializer.Deserialize(reader);
                reader.Close();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool PasteProbes()
        {
            //If we can't paste / paste buffer is bad do nothing
            try
            {
                var deserializer = new XmlSerializer(typeof(Vector3[]));
                var reader = new StringReader(GUIUtility.systemCopyBuffer);
                var pastedProbes = (Vector3[])deserializer.Deserialize(reader);
                reader.Close();

                if (pastedProbes.Length == 0) return false;

                Undo.RegisterCompleteObjectUndo(new UnityEngine.Object[] { m_Group, m_SerializedSelectedProbes }, "Paste Probes");

                var oldLength = m_SourcePositions.Count;

                //Need to convert into local space...
                foreach (var position in pastedProbes)
                {
                    m_SourcePositions.Add(m_Group.transform.InverseTransformPoint(position));
                }

                //Change selection to be the newly pasted probes
                DeselectProbes();
                for (int i = oldLength; i < oldLength + pastedProbes.Length; i++)
                {
                    SelectProbe(i);
                }
                MarkMeshDirty();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool InsertProbe()
        {
            try
            {
                Undo.RegisterCompleteObjectUndo(new UnityEngine.Object[] { m_Group, m_SerializedSelectedProbes }, "Insert Probes");

                var oldLength = m_SourcePositions.Count;

                int idx = m_Selection[0];

                m_SourcePositions.Insert(idx, m_SourcePositions[idx]);

                //Change selection to be the newly pasted probes
                DeselectProbes();

                SelectProbe(idx);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public void RemoveSelectedProbes()
        {
            int selectionCount = m_Selection.Count;
            if (selectionCount == 0)
                return;

            Undo.RegisterCompleteObjectUndo(new UnityEngine.Object[] { m_Group, m_SerializedSelectedProbes }, "Delete Probes");

            var reverseSortedIndicies = m_Selection.OrderByDescending(x => x);
            foreach (var index in reverseSortedIndicies)
            {
                m_SourcePositions.RemoveAt(index);
            }
            DeselectProbes();
            MarkMeshDirty();
        }

        public void RebuildProbes()
        {
            Undo.RegisterCompleteObjectUndo(new UnityEngine.Object[] { m_Group, m_SerializedSelectedProbes }, "Rebuild Probes");

            m_SourcePositions.Clear();

            //todo: generate probes

            Transform trans = m_Group.transform;
            Vector3 max_size = m_Group.volumeSize / 2;
            Vector3 probe_pos = max_size;
            float cell_size = m_Group.cellSize;

            probe_pos = probe_pos / cell_size;
            probe_pos = - new Vector3(Mathf.Floor(probe_pos.x), Mathf.Floor(probe_pos.y), Mathf.Floor(probe_pos.z)) * cell_size;
            Vector3 init_Pos = probe_pos;

            GameObject go = new GameObject();
            Camera cam = go.AddComponent<Camera>();
            RenderTextureDescriptor rtd = new RenderTextureDescriptor(1024, 1024, RenderTextureFormat.ARGBFloat, 24);
            rtd.dimension = UnityEngine.Rendering.TextureDimension.Cube;
            rtd.enableRandomWrite = true;
            RenderTexture rt = new RenderTexture(rtd);
            rt.Create();
            m_Group.GetComponent<DebugHelper>().rt = rt;

            while (probe_pos.x < max_size.x)
            {
                while (probe_pos.y < max_size.y)
                {
                    while (probe_pos.z < max_size.z)
                    {
                        m_SourcePositions.Add(probe_pos);

                        go.transform.position = trans.TransformPoint(probe_pos);

                        cam.cameraType = (CameraType)32;
                        cam.RenderWithShader(Shader.Find("Bake/BakeProbe"), "RenderType");
                        cam.RenderToCubemap(rt);

                        probe_pos.z += cell_size;
                    }
                    probe_pos.y += cell_size;
                    probe_pos.z = init_Pos.z;
                }
                probe_pos.x += cell_size;
                probe_pos.y = init_Pos.y;
                probe_pos.z = init_Pos.z;
            }
            
            //

            DeselectProbes();
            MarkMeshDirty();
        }


        public void PullProbePositions()
        {
            if (m_Group != null && m_SerializedSelectedProbes != null)
            {
                m_SourcePositions = new List<Vector3>(m_Group.probePositions);
                m_Selection = new List<int>(m_SerializedSelectedProbes.m_Selection);
            }
        }

        public void PushProbePositions()
        {
            m_Group.probePositions = m_SourcePositions.ToArray();
            m_SerializedSelectedProbes.m_Selection = m_Selection;
        }

        public void HandleEditMenuHotKeyCommands()
        {
            //Handle other events!
            if (Event.current.type == EventType.ValidateCommand
                || Event.current.type == EventType.ExecuteCommand)
            {
                bool execute = Event.current.type == EventType.ExecuteCommand;
                switch (Event.current.commandName)
                {
                    case EventCommandNames.SoftDelete:
                    case EventCommandNames.Delete:
                        if (execute) RemoveSelectedProbes();
                        Event.current.Use();
                        break;
                    case EventCommandNames.Duplicate:
                        if (execute) DuplicateSelectedProbes();
                        Event.current.Use();
                        break;
                    case EventCommandNames.SelectAll:
                        if (execute)
                            SelectAllProbes();
                        Event.current.Use();
                        break;
                    case EventCommandNames.Cut:
                        if (execute)
                        {
                            CopySelectedProbes();
                            RemoveSelectedProbes();
                        }
                        Event.current.Use();
                        break;
                    case EventCommandNames.Copy:
                        if (execute) CopySelectedProbes();
                        Event.current.Use();
                        break;
                }
            }
        }


        bool have_saved = false;
        public bool OnSceneGUI(Transform transform)
        {
            if (!m_Group.enabled)
                return m_Editing;

            if (Event.current.type == EventType.Layout)
            {
                //If the group has moved / scaled since last frame need to retetra);)
                if (m_LastPosition != m_Group.transform.position
                    || m_LastRotation != m_Group.transform.rotation
                    || m_LastScale != m_Group.transform.localScale)
                {
                    MarkMeshDirty();
                }

                m_LastPosition = m_Group.transform.position;
                m_LastRotation = m_Group.transform.rotation;
                m_LastScale = m_Group.transform.localScale;
            }

            //See if we should enter edit mode!
            bool firstSelect = false;
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                //We have no probes selected and have clicked the mouse... Did we click a probe
                if (SelectedCount == 0)
                {
                    var selected = PointEditor.FindNearest(Event.current.mousePosition, transform, this);
                    var clickedProbe = selected != -1;

                    if (clickedProbe && !m_Editing)
                    {
                        m_Inspector.StartEditMode();
                        m_Editing = true;
                        firstSelect = true;
                    }
                }
            }

            //Need to cache this as select points will use it!
            var mouseUpEvent = Event.current.type == EventType.MouseUp;

            if (m_Editing)
            {
                if (PointEditor.SelectPoints(this, transform, ref m_Selection, firstSelect))
                {
                    Undo.RegisterCompleteObjectUndo(new UnityEngine.Object[] { m_Group, m_SerializedSelectedProbes }, "Select Probes");
                    m_Inspector.Repaint();
                }
                if (SelectedCount > 0)
                {
                    Transform trans = m_Group.transform;
                    Vector3 pos = trans.TransformPoint(SelectedCount > 0 ? GetSelectedPositions()[0] : Vector3.zero);
                    Vector3 newPosition = Handles.DoPositionHandle(pos, Quaternion.identity);

                    if (mouseUpEvent)
                    {
                        have_saved = false;
                    }
                    if (newPosition != pos)
                    {
                        newPosition = trans.InverseTransformPoint(newPosition);
                        pos = trans.InverseTransformPoint(pos);
                        if (!have_saved)
                        {
                            Undo.RegisterCompleteObjectUndo(new UnityEngine.Object[] { m_Group, m_SerializedSelectedProbes }, "Move Probes");
                            have_saved = true;
                        }
                        Vector3[] selectedPositions = GetSelectedPositions();
                        Vector3 delta = newPosition - pos;
                        for (int i = 0; i < selectedPositions.Length; i++)
                            UpdateSelectedPosition(i, selectedPositions[i] + delta);
                        MarkMeshDirty();
                    }
                }
            }

            //Special handling for paste (want to be able to paste when not in edit mode!)

            if ((Event.current.type == EventType.ValidateCommand || Event.current.type == EventType.ExecuteCommand)
                && Event.current.commandName == EventCommandNames.Paste)
            {
                if (Event.current.type == EventType.ValidateCommand)
                {
                    if (CanPasteProbes())
                        Event.current.Use();
                }
                if (Event.current.type == EventType.ExecuteCommand)
                {
                    if (PasteProbes())
                    {
                        Event.current.Use();
                        m_Editing = true;
                    }
                }
            }

            PointEditor.Draw(this, transform, m_Selection, true);


            //volume size
            {
                var color = Handles.color;

                Handles.color = Color.yellow;

                var trans = m_Group.transform;
                var cellSize = m_Group.cellSize / 2;
                bool changed = false;


                Vector3 half_size = m_Group.volumeSize / 2;

                {
                    var handle_pos = trans.TransformPoint(Vector3.right * half_size.x);
                    var poss = Handles.Slider(handle_pos, trans.right, 0.1f, Handles.CubeHandleCap, 0);
                    float new_v = trans.InverseTransformPoint(poss).x;
                    new_v = new_v < cellSize ? cellSize : new_v;
                    if (new_v != half_size.x) { changed = true; half_size.x = new_v; }
                }
                {
                    var handle_pos = trans.TransformPoint(Vector3.left * half_size.x);
                    var poss = Handles.Slider(handle_pos, -trans.right, 0.1f, Handles.CubeHandleCap, 0);
                    float new_v = -trans.InverseTransformPoint(poss).x;
                    new_v = new_v < cellSize ? cellSize : new_v;
                    if (new_v != half_size.x) { changed = true; half_size.x = new_v; }
                }
                {
                    var handle_pos = trans.TransformPoint(Vector3.up * half_size.y);
                    var poss = Handles.Slider(handle_pos, trans.up, 0.1f, Handles.CubeHandleCap, 0);
                    float new_v = trans.InverseTransformPoint(poss).y;
                    new_v = new_v < cellSize ? cellSize : new_v;
                    if (new_v != half_size.y) { changed = true; half_size.y = new_v; }
                }
                {
                    var handle_pos = trans.TransformPoint(Vector3.down * half_size.y);
                    var poss = Handles.Slider(handle_pos, -trans.up, 0.1f, Handles.CubeHandleCap, 0);
                    float new_v = -trans.InverseTransformPoint(poss).y;
                    new_v = new_v < cellSize ? cellSize : new_v;
                    if (new_v != half_size.y) { changed = true; half_size.y = new_v; }
                }
                {
                    var handle_pos = trans.TransformPoint(Vector3.forward * half_size.z);
                    var poss = Handles.Slider(handle_pos, trans.forward, 0.1f, Handles.CubeHandleCap, 0);
                    float new_v = trans.InverseTransformPoint(poss).z;
                    new_v = new_v < cellSize ? cellSize : new_v;
                    if (new_v != half_size.z) { changed = true; half_size.z = new_v; }
                }
                {
                    var handle_pos = trans.TransformPoint(Vector3.back * half_size.z);
                    var poss = Handles.Slider(handle_pos, -trans.forward, 0.1f, Handles.CubeHandleCap, 0);
                    float new_v = -trans.InverseTransformPoint(poss).z;
                    new_v = new_v < cellSize ? cellSize : new_v;
                    if (new_v != half_size.z) { changed = true; half_size.z = new_v; }
                }

                if (mouseUpEvent)
                {
                    have_saved = false;
                }
                if (changed)
                {
                    if (!have_saved)
                    {
                        Undo.RegisterCompleteObjectUndo(new UnityEngine.Object[] { m_Group }, "Change probe volume size");
                        have_saved = true;
                    }
                    m_Group.volumeSize = half_size * 2;
                }
                
                Handles.color = color;
            }


            if (!m_Editing)
                return m_Editing;

            HandleEditMenuHotKeyCommands();

            return m_Editing;
        }


        public void MarkMeshDirty()
        {
            m_Group.RecalcuMesh();
        }

        public Bounds selectedProbeBounds
        {
            get
            {
                List<Vector3> selectedPoints = new List<Vector3>();
                foreach (var idx in m_Selection)
                    selectedPoints.Add(m_SourcePositions[(int)idx]);
                return GetBounds(selectedPoints);
            }
        }

        public Bounds bounds
        {
            get { return GetBounds(m_SourcePositions); }
        }

        private Bounds GetBounds(List<Vector3> positions)
        {
            if (positions.Count == 0)
                return new Bounds();

            if (positions.Count == 1)
                return new Bounds(m_Group.transform.TransformPoint(positions[0]), new Vector3(1f, 1f, 1f));

            return GeometryUtility.CalculateBounds(positions.ToArray(), m_Group.transform.localToWorldMatrix);
        }

        /// Get the world-space position of a specific point
        public Vector3 GetPosition(int idx)
        {
            return m_SourcePositions[idx];
        }

        public Vector3 GetWorldPosition(int idx)
        {
            return m_Group.transform.TransformPoint(m_SourcePositions[idx]);
        }

        public void SetPosition(int idx, Vector3 position)
        {
            if (m_SourcePositions[idx] == position)
                return;

            m_SourcePositions[idx] = position;
        }

        private static readonly Color kCloudColor = new Color(200f / 255f, 0, 20f / 255f, 0.75f);
        private static readonly Color kSelectedCloudColor = new Color(.3f, 0, 1, 1);

        public Color GetDefaultColor()
        {
            return kCloudColor;
        }

        public Color GetSelectedColor()
        {
            return kSelectedCloudColor;
        }

        public float GetPointScale()
        {
            var t = typeof(Lightmapping).Assembly.GetType("UnityEditor.AnnotationUtility").GetProperty("iconSize", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            return 10.0f * (float)(t);
        }

        public Vector3[] GetSelectedPositions()
        {
            var selectedCount = SelectedCount;
            var result = new Vector3[selectedCount];
            for (int i = 0; i < selectedCount; i++)
            {
                result[i] = m_SourcePositions[m_Selection[i]];
            }
            return result;
        }

        public Vector3[] GetSelectedPositionsInSM()
        {
            var selectedCount = SelectedCount;
            var result = new Vector3[selectedCount];
            for (int i = 0; i < selectedCount; i++)
            {
                result[i] = m_Group.probePositions[m_Selection[i]];
            }
            return result;
        }

        public void UpdateSelectedPosition(int idx, Vector3 position)
        {
            if (idx > (SelectedCount - 1))
                return;

            m_SourcePositions[m_Selection[idx]] = position;
        }

        public IEnumerable<Vector3> GetPositions()
        {
            return m_SourcePositions;
        }

        public Vector3[] GetUnselectedPositions()
        {
            var totalProbeCount = Count;
            var selectedProbeCount = SelectedCount;

            if (selectedProbeCount == totalProbeCount)
            {
                return new Vector3[0];
            }
            else if (selectedProbeCount == 0)
            {
                return m_SourcePositions.ToArray();
            }
            else
            {
                var selectionList = new bool[totalProbeCount];

                // Mark everything unselected
                for (int i = 0; i < totalProbeCount; i++)
                {
                    selectionList[i] = false;
                }

                // Mark selected
                for (int i = 0; i < selectedProbeCount; i++)
                {
                    selectionList[m_Selection[i]] = true;
                }

                // Get remaining unselected
                var result = new Vector3[totalProbeCount - selectedProbeCount];
                var unselectedCount = 0;
                for (int i = 0; i < totalProbeCount; i++)
                {
                    if (selectionList[i] == false)
                    {
                        result[unselectedCount++] = m_SourcePositions[i];
                    }
                }

                return result;
            }
        }

        /// How many points are there in the array.
        public int Count { get { return m_SourcePositions.Count; } }


        /// How many points are selected in the array.
        public int SelectedCount { get { return m_Selection.Count; } }
    }




    [CustomEditor(typeof(LightProbe))]
    class LightProbeEditor : Editor
    {
        private static class Styles
        {
            public static readonly GUIContent showVolume = EditorGUIUtility.TrTextContent("Show volume in scene view");
            public static readonly GUIContent cellSize = EditorGUIUtility.TrTextContent("Cell size");
            public static readonly GUIContent volumeSize = EditorGUIUtility.TrTextContent("Volume size");
            public static readonly GUIContent rebuild = EditorGUIUtility.TrTextContent("Rebuild probes automatically(todo)");
            public static readonly GUIContent selectedProbePosition = EditorGUIUtility.TrTextContent("Selected Probe Position", "The local position of this ponit relative to parent.");
            public static readonly GUIContent addPoint = EditorGUIUtility.TrTextContent("Add Probe");
            public static readonly GUIContent deleteSelected = EditorGUIUtility.TrTextContent("Delete Selected");
            public static readonly GUIContent selectAll = EditorGUIUtility.TrTextContent("Select All");
            public static readonly GUIContent duplicateSelected = EditorGUIUtility.TrTextContent("Duplicate Selected");
            public static readonly GUIContent rebake = EditorGUIUtility.TrTextContent("Rebake probes of scene(todo)", "Will rebake all probes changed in the scene, not only this light probe component.");
            public static readonly GUIContent editModeButton;

            static Styles()
            {
                editModeButton = EditorGUIUtility.IconContent("EditCollider");
            }
        }
        private PointEditr m_Editor;

        public void OnEnable()
        {
            m_Editor = new PointEditr(target as LightProbe, this);
            m_Editor.PullProbePositions();
            m_Editor.DeselectProbes();
            m_Editor.PushProbePositions();
            SceneView.onSceneGUIDelegate += OnSceneGUIDelegate;
            Undo.undoRedoPerformed += UndoRedoPerformed;
            EditMode.onEditModeStartDelegate += OnEditModeStarted;
            EditMode.onEditModeEndDelegate += OnEditModeEnded;
        }

        private void OnEditModeEnded(Editor owner)
        {
            if (owner == this)
            {
                EndEditProbes();
            }
        }

        private void OnEditModeStarted(Editor owner, EditMode.SceneViewEditMode mode)
        {
            if (owner == this && mode == EditMode.SceneViewEditMode.LightProbeGroup)
            {
                StartEditProbes();
            }
        }

        public void StartEditMode()
        {
            EditMode.ChangeEditMode(EditMode.SceneViewEditMode.LightProbeGroup, m_Editor.bounds, this);
        }

        private void StartEditProbes()
        {
            if (m_EditingProbes)
                return;

            m_EditingProbes = true;
            m_Editor.SetEditing(true);
            Tools.hidden = true;
            SceneView.RepaintAll();
        }

        private void EndEditProbes()
        {
            if (!m_EditingProbes)
                return;

            m_Editor.DeselectProbes();
            m_Editor.SetEditing(false);
            m_EditingProbes = false;
            Tools.hidden = false;
            SceneView.RepaintAll();
        }

        public void OnDisable()
        {
            EndEditProbes();
            Undo.undoRedoPerformed -= UndoRedoPerformed;
            SceneView.onSceneGUIDelegate -= OnSceneGUIDelegate;
            if (target != null)
            {
                m_Editor.PushProbePositions();
                m_Editor = null;
            }
        }

        private void UndoRedoPerformed()
        {
            // Update the cached probe positions from the ones just restored in the LightProbeGroup
            m_Editor.PullProbePositions();

            m_Editor.MarkMeshDirty();
        }

        private bool m_EditingProbes;
        private bool m_ShouldFocus;
        public override void OnInspectorGUI()
        {
            m_Editor.PullProbePositions();

            var lp = target as LightProbe;
            if (!lp) return;


            lp.showVolumeInScene = EditorGUILayout.Toggle(Styles.showVolume, lp.showVolumeInScene);

            lp.cellSize = EditorGUILayout.FloatField(Styles.cellSize, lp.cellSize);
            lp.volumeSize = EditorGUILayout.Vector3Field(Styles.volumeSize, lp.volumeSize);

            if (GUILayout.Button(Styles.rebuild))
            {
                m_Editor.RebuildProbes();
            }

            GUILayout.Space(10);

            EditMode.DoEditModeInspectorModeButton(UnityEditorInternal.EditMode.SceneViewEditMode.LightProbeGroup, "Edit Probes manually", EditorGUIUtility.IconContent("EditCollider"), this.m_Editor.bounds, this);

            GUILayout.Space(3);
            EditorGUI.BeginDisabledGroup(EditMode.editMode != EditMode.SceneViewEditMode.LightProbeGroup);

            //bool performDeringing = EditorGUILayout.Toggle(Styles.performDeringing, m_Editor.GetDeringProbes());
            //m_Editor.SetDeringProbes(performDeringing);


            EditorGUI.BeginChangeCheck();

            EditorGUI.BeginDisabledGroup(m_Editor.SelectedCount == 0);
            Vector3 pos = m_Editor.SelectedCount > 0 ? m_Editor.GetSelectedPositions()[0] : Vector3.zero;
            Vector3 newPosition = EditorGUILayout.Vector3Field(Styles.selectedProbePosition, pos);
            if (newPosition != pos)
            {
                Vector3[] selectedPositions = m_Editor.GetSelectedPositions();
                Vector3 delta = newPosition - pos;
                for (int i = 0; i < selectedPositions.Length; i++)
                    m_Editor.UpdateSelectedPosition(i, selectedPositions[i] + delta);
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.Space(3);

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();

            if (GUILayout.Button(Styles.addPoint))
            {
                var position = Vector3.zero;
                //if (SceneView.lastActiveSceneView)
                //{
                //    var probeGroup = target as SplineMesh;
                //    if (probeGroup) position = probeGroup.transform.InverseTransformPoint(position);
                //}
                StartEditProbes();
                m_Editor.DeselectProbes();
                m_Editor.AddProbe(position);
            }

            if (GUILayout.Button(Styles.deleteSelected))
            {
                StartEditProbes();
                m_Editor.RemoveSelectedProbes();
            }
            GUILayout.EndVertical();
            GUILayout.BeginVertical();

            if (GUILayout.Button(Styles.selectAll))
            {
                StartEditProbes();
                m_Editor.SelectAllProbes();
            }

            if (GUILayout.Button(Styles.duplicateSelected))
            {
                StartEditProbes();
                m_Editor.DuplicateSelectedProbes();
            }

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            EditorGUI.EndDisabledGroup();

            GUILayout.Space(10);

            if (GUILayout.Button(Styles.rebake))
            {
                //todo: rebake probes of influnced groups


                //
            }


            m_Editor.HandleEditMenuHotKeyCommands();
            m_Editor.PushProbePositions();

            if (EditorGUI.EndChangeCheck())
            {
                m_Editor.MarkMeshDirty();
                SceneView.RepaintAll();
            }
        }

        internal Bounds GetWorldBoundsOfTarget(UnityEngine.Object targetObject)
        {
            return m_Editor.bounds;
        }

        private void InternalOnSceneView()
        {
            if (SceneView.lastActiveSceneView != null)
            {
                if (m_ShouldFocus)
                {
                    m_ShouldFocus = false;
                    SceneView.lastActiveSceneView.FrameSelected();
                }
            }

            m_Editor.PullProbePositions();
            var lpg = target as LightProbe;
            if (lpg != null)
            {
                if (m_Editor.OnSceneGUI(lpg.transform))
                    StartEditProbes();
                else
                    EndEditProbes();
            }
            m_Editor.PushProbePositions();
        }

        public void OnSceneGUI()
        {
            if (Event.current.type != EventType.Repaint)
                InternalOnSceneView();
        }

        public void OnSceneGUIDelegate(SceneView sceneView)
        {
            if (Event.current.type == EventType.Repaint)
                InternalOnSceneView();
        }

        public bool HasFrameBounds()
        {
            return m_Editor.SelectedCount > 0;
        }

        public Bounds OnGetFrameBounds()
        {
            return m_Editor.selectedProbeBounds;
        }
    }
}
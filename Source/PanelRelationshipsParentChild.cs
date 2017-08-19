﻿using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.Sound;
namespace EdB.PrepareCarefully {
    public class PanelRelationshipsParentChild : PanelBase {
        public delegate void AddParentToGroupHandler(CustomParentChildGroup group, CustomParentChildPawn pawn);
        public delegate void RemoveParentFromGroupHandler(CustomParentChildGroup group, CustomParentChildPawn pawn);
        public delegate void AddChildToGroupHandler(CustomParentChildGroup group, CustomParentChildPawn pawn);
        public delegate void RemoveChildFromGroupHandler(CustomParentChildGroup group, CustomParentChildPawn pawn);
        public delegate void RemoveGroupHandler(CustomParentChildGroup group);
        public delegate void AddGroupHandler(CustomParentChildGroup group);

        public event AddParentToGroupHandler ParentAddedToGroup;
        public event RemoveParentFromGroupHandler ParentRemovedFromGroup;
        public event AddChildToGroupHandler ChildAddedToGroup;
        public event RemoveChildFromGroupHandler ChildRemovedFromGroup;
        public event AddGroupHandler GroupAdded;

        private ScrollViewHorizontal scrollView = new ScrollViewHorizontal();
        private Rect RectScrollView;
        private float SpacingGroup = 16;
        private Vector2 SizePawn = new Vector2(90, 90);
        private Vector2 SizeArrowHead = new Vector2(20, 10);
        private Vector2 SizeAddButton = new Vector2(16, 16);
        private Vector2 SizeDeleteButton = new Vector2(12, 12);
        private Vector2 SizeArrowBase = new Vector2(12, 10);
        private float PaddingBox = 12;
        private float PaddingAddButton = 4;
        private float PaddingDeleteButton = 6;
        private float SpacingPawn = 12;
        private float SpacingArrow = 12;
        private float SpacingGender = 9;
        private float SizeChildOffset = 24;
        private Vector2 SizeGender = new Vector2(48,48);

        private Color ColorParent = new Color(69f / 255f, 70f / 255f, 72f / 255f);
        private Color ColorParentEmpty = new Color(69f / 255f, 70f / 255f, 72f / 255f, 0.25f);
        private Color ColorChild = new Color(22f / 255f, 22f / 255f, 23f / 255f);
        private Color ColorChildEmpty = new Color(22f / 255f, 22f / 255f, 23f / 255f, 0.40f);

        private HashSet<Backstory> visibleBackstories = new HashSet<Backstory>();
        private List<CustomParentChildPawn> newPawns = new List<CustomParentChildPawn>();

        public PanelRelationshipsParentChild() {
            // TODO: Pull this out and put it in a utility somewhere, i.e. ProviderBackstory.
            foreach (Backstory backstory in BackstoryDatabase.allBackstories.Values) {
                if (backstory.identifier.StartsWith("FactionLeader")) {
                    visibleBackstories.Add(backstory);
                }
            }
            // Add a male and a female pawn to the new hidden pawn list.
            newPawns.Add(CreateNewHiddenPawn(Gender.Female));
            newPawns.Add(CreateNewHiddenPawn(Gender.Male));
        }
        public override string PanelHeader {
            get {
                return "EdB.PC.Panel.RelationshipsParentChild.Title".Translate();
            }
        }
        public override void Resize(Rect rect) {
            base.Resize(rect);
            Vector2 padding = Style.SizePanelPadding;
            RectScrollView = new Rect(padding.x, BodyRect.y, rect.width - padding.x * 2, rect.height - BodyRect.y - padding.y);
        }
        protected override void DrawPanelContent(State state) {
            base.DrawPanelContent(state);
            float cursor = 0;

            GUI.color = Color.white;

            GUI.BeginGroup(RectScrollView);
            scrollView.Begin(new Rect(Vector2.zero, RectScrollView.size));
            try {
                foreach (var group in PrepareCarefully.Instance.RelationshipManager.ParentChildGroups) {
                    cursor = DrawGroup(cursor, group);
                }
                cursor = DrawNextGroup(cursor);
            }
            finally {
                scrollView.End(cursor);
                GUI.EndGroup();
            }
            
            // Remove any parents or children that were marked for removal.
            if (parentsToRemove.Count > 0) {
                foreach (var pair in parentsToRemove) {
                    ParentRemovedFromGroup(pair.Group, pair.Pawn);
                }
                parentsToRemove.Clear();
            }
            if (childrenToRemove.Count > 0) {
                foreach (var pair in childrenToRemove) {
                    ChildRemovedFromGroup(pair.Group, pair.Pawn);
                }
                childrenToRemove.Clear();
            }
        }

        protected CustomParentChildPawn ReplaceNewHiddenCharacter(int index) {
            var pawn = newPawns[index];
            newPawns[index] = CreateNewHiddenPawn(pawn.Pawn.Gender);
            CustomParentChildPawn result = PrepareCarefully.Instance.RelationshipManager.AddHiddenParentChildPawn(pawn.Pawn);
            result.Index = PrepareCarefully.Instance.RelationshipManager.NextHiddenParentChildIndex;
            return result;
        }

        protected CustomParentChildPawn CreateNewHiddenPawn(Gender gender) {
            CustomPawn pawn = new CustomPawn(new Randomizer().GeneratePawn(new PawnGenerationRequestWrapper() {
                FixedGender = gender
            }.Request));
            CustomParentChildPawn result = new CustomParentChildPawn(pawn, true);
            result.Name = "EdB.PC.AddParentChild.NewHiddenCharacter".Translate();
            return result;
        }

        protected struct PawnGroupPair {
            public CustomParentChildPawn Pawn;
            public CustomParentChildGroup Group;
            public PawnGroupPair(CustomParentChildPawn Pawn, CustomParentChildGroup Group) {
                this.Pawn = Pawn;
                this.Group = Group;
            }
        }

        private List<CustomParentChildGroup> groupsToRemove = new List<CustomParentChildGroup>();
        private List<PawnGroupPair> parentsToRemove = new List<PawnGroupPair>();
        private List<PawnGroupPair> childrenToRemove = new List<PawnGroupPair>();
        protected float DrawGroup(float cursor, CustomParentChildGroup group) {
            int parentBoxCount = group.Parents.Count + 1;
            int childBoxCount = group.Children.Count + 1;
            float widthOfParents = parentBoxCount * SizePawn.x + (SpacingPawn * (parentBoxCount - 1)) + (PaddingBox * 2);
            float widthOfChildren = SizeChildOffset + (childBoxCount * SizePawn.x) + (SpacingPawn * (childBoxCount - 1)) + (PaddingBox * 2);
            float width = Mathf.Max(widthOfChildren, widthOfParents);
            float height = PaddingBox * 2 + SpacingArrow * 2 + SizeArrowBase.y + SizeArrowHead.y + SizePawn.y * 2;
            Rect rect = new Rect(cursor, 0, width, height);

            GUI.color = Style.ColorPanelBackgroundItem;
            GUI.DrawTexture(rect, BaseContent.WhiteTex);
            
            GUI.BeginGroup(rect);
            try {
                Color arrowColor = group.Parents.Count > 0 ? ColorParent : ColorParentEmpty;
                Rect firstParentPawnRect = new Rect(PaddingBox, PaddingBox, SizePawn.x, SizePawn.y);
                Rect parentPawnRect = firstParentPawnRect;
                float arrowBaseRightParent = 0;
                foreach (var parent in group.Parents) {
                    GUI.color = ColorParent;
                    GUI.DrawTexture(parentPawnRect, BaseContent.WhiteTex);

                    Rect arrowBaseRect = new Rect(parentPawnRect.MiddleX() - SizeArrowBase.HalfX(), parentPawnRect.yMax, SizeArrowBase.x, SpacingArrow);
                    arrowBaseRightParent = arrowBaseRect.xMax;
                    GUI.DrawTexture(arrowBaseRect, BaseContent.WhiteTex);

                    DrawPortrait(parent, parentPawnRect);

                    if (parentPawnRect.Contains(Event.current.mousePosition)) {
                        Rect deleteRect = new Rect(parentPawnRect.xMax - PaddingDeleteButton - SizeDeleteButton.x, parentPawnRect.y + PaddingDeleteButton, SizeDeleteButton.x, SizeDeleteButton.y);
                        Style.SetGUIColorForButton(deleteRect);
                        GUI.DrawTexture(deleteRect, Textures.TextureButtonDelete);
                        if (Widgets.ButtonInvisible(deleteRect)) {
                            parentsToRemove.Add(new PawnGroupPair(parent, group));
                        }
                    }

                    parentPawnRect.x += SizePawn.x + SpacingPawn;
                }

                // If there's no parent, we still want to draw the arrow that points from the first parent box to the first child.
                // Normally, we would have drawn it when we drew the parent, but since there's aren't any parents, we have to call
                // it out here and draw it separately.
                if (group.Parents.Count == 0) {
                    GUI.color = arrowColor;
                    Rect arrowBaseRect = new Rect(parentPawnRect.MiddleX() - SizeArrowBase.HalfX(), parentPawnRect.yMax, SizeArrowBase.x, SpacingArrow);
                    GUI.DrawTexture(arrowBaseRect, BaseContent.WhiteTex);
                    arrowBaseRightParent = arrowBaseRect.x;
                }

                GUI.color = ColorParentEmpty;
                GUI.DrawTexture(parentPawnRect, BaseContent.WhiteTex);
                Rect addParentRect = new Rect(parentPawnRect.xMax - PaddingAddButton - SizeAddButton.x, parentPawnRect.y + PaddingAddButton, SizeAddButton.x, SizeAddButton.y);
                Style.SetGUIColorForButton(parentPawnRect);
                GUI.DrawTexture(addParentRect, Textures.TextureButtonAdd);
                if (Widgets.ButtonInvisible(parentPawnRect)) {
                    ShowParentDialogForGroup(group, null, (CustomParentChildPawn pawn) => {
                        ParentAddedToGroup(group, pawn);
                    });
                }

                Rect childPawnRect = new Rect(PaddingBox + SizeChildOffset, PaddingBox + SizePawn.y + SizeArrowBase.y + SizeArrowHead.y + SpacingArrow * 2, SizePawn.x, SizePawn.y);
                float arrowBaseLeft = firstParentPawnRect.MiddleX() - SizeArrowBase.HalfX();
                float arrowBaseRightChild = arrowBaseLeft + SizeArrowBase.x;
                foreach (var child in group.Children) {
                    GUI.color = ColorChild;
                    GUI.DrawTexture(childPawnRect, BaseContent.WhiteTex);

                    GUI.color = arrowColor;
                    Rect arrowBaseRect = new Rect(childPawnRect.MiddleX() - SizeArrowBase.HalfX(), childPawnRect.y - SizeArrowHead.y - SpacingArrow, SizeArrowBase.x, SpacingArrow);
                    GUI.DrawTexture(arrowBaseRect, BaseContent.WhiteTex);

                    Rect arrowHeadRect = new Rect(childPawnRect.MiddleX() - SizeArrowHead.HalfX(), childPawnRect.y - SizeArrowHead.y, SizeArrowHead.x, SizeArrowHead.y);
                    GUI.DrawTexture(arrowHeadRect, Textures.TextureArrowDown);
                    
                    DrawPortrait(child, childPawnRect);

                    if (childPawnRect.Contains(Event.current.mousePosition)) {
                        Rect deleteRect = new Rect(childPawnRect.xMax - PaddingDeleteButton - SizeDeleteButton.x, childPawnRect.y + PaddingDeleteButton, SizeDeleteButton.x, SizeDeleteButton.y);
                        Style.SetGUIColorForButton(deleteRect);
                        GUI.DrawTexture(deleteRect, Textures.TextureButtonDelete);
                        if (Widgets.ButtonInvisible(deleteRect)) {
                            childrenToRemove.Add(new PawnGroupPair(child, group));
                        }
                    }

                    childPawnRect.x += SizePawn.x + SpacingPawn;
                    arrowBaseRightChild = arrowBaseRect.xMax;
                }
                
                // If there's no children, we still want to draw the arrow that points to the first child.
                // Normally, we would have drawn it when we drew the children, but since there's aren't any children,
                // we have to call it out here and draw it separately.
                if (group.Children.Count == 0) {
                    GUI.color = arrowColor;
                    Rect arrowBaseRect = new Rect(childPawnRect.MiddleX() - SizeArrowBase.HalfX(), childPawnRect.y - SizeArrowHead.y - SpacingArrow, SizeArrowBase.x, SpacingArrow);
                    GUI.DrawTexture(arrowBaseRect, BaseContent.WhiteTex);
                    Rect arrowHeadRect = new Rect(childPawnRect.MiddleX() - SizeArrowHead.HalfX(), childPawnRect.y - SizeArrowHead.y, SizeArrowHead.x, SizeArrowHead.y);
                    GUI.DrawTexture(arrowHeadRect, Textures.TextureArrowDown);
                    arrowBaseRightChild = arrowBaseRect.xMax;
                }

                GUI.color = ColorChildEmpty;
                GUI.DrawTexture(childPawnRect, BaseContent.WhiteTex);
                Rect addChildRect = new Rect(childPawnRect.xMax - PaddingAddButton - SizeAddButton.x, childPawnRect.y + PaddingAddButton, SizeAddButton.x, SizeAddButton.y);
                Style.SetGUIColorForButton(childPawnRect);
                GUI.DrawTexture(addChildRect, Textures.TextureButtonAdd);
                if (Widgets.ButtonInvisible(childPawnRect)) {
                    ShowChildDialogForGroup(group, null, (CustomParentChildPawn pawn) => {
                        ChildAddedToGroup(group, pawn);
                    });
                }

                float arrowBaseRight = Mathf.Max(arrowBaseRightChild, arrowBaseRightParent);
                Rect arrowBaseLineRect = new Rect(arrowBaseLeft, PaddingBox + SizePawn.y + SpacingArrow, arrowBaseRight - arrowBaseLeft, SizeArrowBase.y);
                GUI.color = arrowColor;
                GUI.DrawTexture(arrowBaseLineRect, BaseContent.WhiteTex);
            }
            finally {
                GUI.EndGroup();
            }

            GUI.color = Color.white;
            cursor += width + SpacingGroup;

            return cursor;
        }
        protected void DrawPortrait(CustomParentChildPawn pawn, Rect rect) {
            Rect parentNameRect = new Rect(rect.x, rect.yMax - 34, rect.width, 26);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperCenter;
            GUI.color = Style.ColorText;
            Widgets.Label(parentNameRect, pawn.Name);
            GUI.color = Color.white;

            Rect parentProfessionRect = new Rect(rect.x, rect.yMax - 18, rect.width, 18);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.LowerCenter;
            GUI.color = Style.ColorText;
            Widgets.Label(parentProfessionRect, GetProfessionLabel(pawn));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            if (!pawn.Hidden) {
                Rect parentPortraitRect = rect.InsetBy(6);
                parentPortraitRect.y -= 8;
                var parentPortraitTexture = pawn.Pawn.GetPortrait(parentPortraitRect.size);
                GUI.DrawTexture(parentPortraitRect.OffsetBy(0, -4), parentPortraitTexture);
            }
            else {
                GUI.color = Style.ColorButton;
                Rect parentPortraitRect = new Rect(rect.MiddleX() - SizeGender.HalfX(), rect.y + SpacingGender, SizeGender.x, SizeGender.y);
                if (pawn.Pawn.Gender == Gender.Female) {
                    GUI.DrawTexture(parentPortraitRect, Textures.TextureGenderFemaleLarge);
                }
                else if (pawn.Pawn.Gender == Gender.Male) {
                    GUI.DrawTexture(parentPortraitRect, Textures.TextureGenderMaleLarge);
                }
                else {
                    GUI.DrawTexture(parentPortraitRect, Textures.TextureGenderlessLarge);
                }
            }
            
            TooltipHandler.TipRegion(rect, GetTooltipText(pawn));
        }
        protected string GetTooltipText(CustomParentChildPawn parentChildPawn) {
            CustomPawn pawn = parentChildPawn.Pawn;
            string description;
            if (!parentChildPawn.Hidden) {
                string age = pawn.BiologicalAge != pawn.ChronologicalAge ?
                    "EdB.PC.Pawn.AgeWithChronological".Translate(new object[] { pawn.BiologicalAge, pawn.ChronologicalAge }) :
                    "EdB.PC.Pawn.AgeWithoutChronological".Translate(new object[] { pawn.BiologicalAge });
                description = pawn.Gender != Gender.None ?
                    "EdB.PC.Pawn.PawnDescriptionWithGender".Translate(new object[] { pawn.ProfessionLabel, pawn.Gender.GetLabel(), age }) :
                    "EdB.PC.Pawn.PawnDescriptionNoGender".Translate(new object[] { pawn.ProfessionLabel, age });
            }
            else {
                string profession = "EdB.PC.Pawn.HiddenPawnProfession".Translate();
                description = pawn.Gender != Gender.None ?
                    "EdB.PC.Pawn.HiddenPawnDescriptionWithGender".Translate(new object[] { profession, pawn.Gender.GetLabel() }) :
                    "EdB.PC.Pawn.HiddenPawnDescriptionNoGender".Translate(new object[] { profession });
            }
            return parentChildPawn.FullName + "\n" + description;
        }
        protected List<WidgetTable<CustomParentChildPawn>.RowGroup> rowGroups = new List<WidgetTable<CustomParentChildPawn>.RowGroup>();
        protected void ShowParentDialogForGroup(CustomParentChildGroup group, CustomParentChildPawn selected, Action<CustomParentChildPawn> action) {
            CustomParentChildPawn selectedPawn = selected;
            HashSet<CustomParentChildPawn> disabled = new HashSet<CustomParentChildPawn>();
            if (group != null) {
                disabled.AddRange(group.Parents);
                disabled.AddRange(group.Children);
            }
            rowGroups.Clear();
            rowGroups.Add(new WidgetTable<CustomParentChildPawn>.RowGroup("EdB.PC.AddParentChild.Header.SelectColonist".Translate(), PrepareCarefully.Instance.RelationshipManager.ColonyPawns));
            rowGroups.Add(new WidgetTable<CustomParentChildPawn>.RowGroup("EdB.PC.AddParentChild.Header.SelectHidden".Translate(), PrepareCarefully.Instance.RelationshipManager.HiddenPawns));
            WidgetTable<CustomParentChildPawn>.RowGroup newPawnGroup = new WidgetTable<CustomParentChildPawn>.RowGroup(null, newPawns);
            rowGroups.Add(newPawnGroup);
            DialogSelectParentChildPawn pawnDialog = new DialogSelectParentChildPawn() {
                HeaderLabel = "EdB.PC.AddParentChild.Header.AddParent".Translate(),
                SelectAction = (CustomParentChildPawn pawn) => { selectedPawn = pawn; },
                RowGroups = rowGroups,
                DisabledPawns = disabled,
                ConfirmValidation = () => {
                    if (selectedPawn == null) {
                        return "EdB.PC.AddParentChild.Error.ParentRequired";
                    }
                    else {
                        return null;
                    }
                },
                CloseAction = () => {
                    // If the user selected a new pawn, replace the pawn in the new pawn list with another one.
                    int index = newPawnGroup.Rows.FirstIndexOf((CustomParentChildPawn p) => {
                        return p == selectedPawn;
                    });
                    if (index > -1 && index < newPawns.Count) {
                        selectedPawn = ReplaceNewHiddenCharacter(index);
                    }
                    action(selectedPawn);
                }
            };
            Find.WindowStack.Add(pawnDialog);
        }
        protected void ShowChildDialogForGroup(CustomParentChildGroup group, CustomParentChildPawn selected, Action<CustomParentChildPawn> action) {
            CustomParentChildPawn selectedPawn = selected;
            HashSet<CustomParentChildPawn> disabled = new HashSet<CustomParentChildPawn>();
            if (group != null) {
                disabled.AddRange(group.Parents);
                disabled.AddRange(group.Children);
            }
            rowGroups.Clear();
            rowGroups.Add(new WidgetTable<CustomParentChildPawn>.RowGroup("EdB.PC.AddParentChild.Header.SelectColonist".Translate(), PrepareCarefully.Instance.RelationshipManager.ColonyPawns));
            rowGroups.Add(new WidgetTable<CustomParentChildPawn>.RowGroup("EdB.PC.AddParentChild.Header.SelectHidden".Translate(), PrepareCarefully.Instance.RelationshipManager.HiddenPawns));
            WidgetTable<CustomParentChildPawn>.RowGroup newPawnGroup = new WidgetTable<CustomParentChildPawn>.RowGroup(null, newPawns);
            rowGroups.Add(newPawnGroup);
            DialogSelectParentChildPawn pawnDialog = new DialogSelectParentChildPawn() {
                HeaderLabel = "EdB.PC.AddParentChild.Header.AddChild".Translate(),
                SelectAction = (CustomParentChildPawn pawn) => { selectedPawn = pawn; },
                RowGroups = rowGroups,
                DisabledPawns = disabled,
                ConfirmValidation = () => {
                    if (selectedPawn == null) {
                        return "EdB.PC.AddParentChild.Error.ChildRequired";
                    }
                    else {
                        return null;
                    }
                },
                CloseAction = () => {
                    // If the user selected a new pawn, replace the pawn in the new pawn list with another one.
                    int index = newPawnGroup.Rows.FirstIndexOf((CustomParentChildPawn p) => {
                        return p == selectedPawn;
                    });
                    if (index > -1 && index < newPawns.Count) {
                        selectedPawn = ReplaceNewHiddenCharacter(index);
                    }
                    action(selectedPawn);
                }
            };
            Find.WindowStack.Add(pawnDialog);
        }
        protected float DrawNextGroup(float cursor) {
            int parentBoxCount = 1;
            int childBoxCount = 1;
            float widthOfParents = parentBoxCount * SizePawn.x + (SpacingPawn * (parentBoxCount - 1)) + (PaddingBox * 2);
            float widthOfChildren = SizeChildOffset + (childBoxCount * SizePawn.x) + (SpacingPawn * (childBoxCount - 1)) + (PaddingBox * 2);
            float width = Mathf.Max(widthOfChildren, widthOfParents);
            float height = PaddingBox * 2 + SpacingArrow * 2 + SizeArrowBase.y + SizeArrowHead.y + SizePawn.y * 2;
            Rect rect = new Rect(cursor, 0, width, height);

            GUI.color = Style.ColorPanelBackgroundItem;
            GUI.DrawTexture(rect, BaseContent.WhiteTex);

            GUI.BeginGroup(rect);
            try {
                Rect parentPawnRect = new Rect(PaddingBox, PaddingBox, SizePawn.x, SizePawn.y);

                GUI.color = ColorParentEmpty;
                GUI.DrawTexture(parentPawnRect, BaseContent.WhiteTex);
                Rect addParentRect = new Rect(parentPawnRect.xMax - PaddingAddButton - SizeAddButton.x, parentPawnRect.y + PaddingAddButton, SizeAddButton.x, SizeAddButton.y);
                Style.SetGUIColorForButton(parentPawnRect);
                GUI.DrawTexture(addParentRect, Textures.TextureButtonAdd);
                if (Widgets.ButtonInvisible(parentPawnRect)) {
                    ShowParentDialogForGroup(null, null, (CustomParentChildPawn pawn) => {
                        CustomParentChildGroup group = new CustomParentChildGroup();
                        group.Parents.Add(pawn);
                        GroupAdded(group);
                    });
                }

                GUI.color = ColorParentEmpty;
                Rect arrowBaseRect = new Rect(parentPawnRect.MiddleX() - SizeArrowBase.HalfX(), parentPawnRect.yMax, SizeArrowBase.x, SpacingArrow);
                GUI.DrawTexture(arrowBaseRect, BaseContent.WhiteTex);

                GUI.color = ColorChildEmpty;
                Rect childPawnRect = new Rect(PaddingBox + SizeChildOffset, PaddingBox + SizePawn.y + SizeArrowBase.y + SizeArrowHead.y + SpacingArrow * 2, SizePawn.x, SizePawn.y);
                float arrowBaseLeft = parentPawnRect.MiddleX() - SizeArrowBase.HalfX();
                GUI.DrawTexture(childPawnRect, BaseContent.WhiteTex);
                Rect addChildRect = new Rect(childPawnRect.xMax - PaddingAddButton - SizeAddButton.x, childPawnRect.y + PaddingAddButton, SizeAddButton.x, SizeAddButton.y);
                Style.SetGUIColorForButton(childPawnRect);
                GUI.DrawTexture(addChildRect, Textures.TextureButtonAdd);
                if (Widgets.ButtonInvisible(childPawnRect)) {
                    ShowChildDialogForGroup(null, null, (CustomParentChildPawn pawn) => {
                        CustomParentChildGroup group = new CustomParentChildGroup();
                        group.Children.Add(pawn);
                        GroupAdded(group);
                    });
                }

                GUI.color = ColorParentEmpty;
                arrowBaseRect = new Rect(childPawnRect.MiddleX() - SizeArrowBase.HalfX(), childPawnRect.y - SizeArrowHead.y - SpacingArrow, SizeArrowBase.x, SpacingArrow);
                GUI.DrawTexture(arrowBaseRect, BaseContent.WhiteTex);
                float arrowBaseRightChild = arrowBaseRect.xMax;

                Rect arrowHeadRect = new Rect(childPawnRect.MiddleX() - SizeArrowHead.HalfX(), childPawnRect.y - SizeArrowHead.y, SizeArrowHead.x, SizeArrowHead.y);
                GUI.DrawTexture(arrowHeadRect, Textures.TextureArrowDown);
                
                Rect arrowBaseLineRect = new Rect(arrowBaseLeft, PaddingBox + SizePawn.y + SpacingArrow, arrowBaseRightChild - arrowBaseLeft, SizeArrowBase.y);
                GUI.DrawTexture(arrowBaseLineRect, BaseContent.WhiteTex);
            }
            finally {
                GUI.EndGroup();
            }

            GUI.color = Color.white;
            cursor += width + SpacingGroup;

            return cursor;
        }
        private string GetProfessionLabel(CustomParentChildPawn pawn) {
            if (!pawn.Hidden) {
                return pawn.Pawn.ProfessionLabelShort;
            }
            if (pawn.Pawn.IsAdult && visibleBackstories.Contains(pawn.Pawn.Adulthood)) {
                return pawn.Pawn.ProfessionLabelShort;
            }
            else if (!pawn.Pawn.IsAdult && visibleBackstories.Contains(pawn.Pawn.Childhood)) {
                return pawn.Pawn.ProfessionLabelShort;
            }
            else {
                return "Unknown";
            }
        }
    }
}

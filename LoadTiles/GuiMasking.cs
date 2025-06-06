using System;
using System.Collections.Generic;
using Eto.Drawing;
using Eto.Forms;
using Rhino;

namespace LoadTiles;

public class MaskingDialog : Dialog<bool> {
    private MaskingCommand maskingCommand;
    private RhinoDoc doc;
    private Panel objectsPanel;
    private Dictionary<Guid, Panel> objectPanels;
    private Guid? highlightedObject = null;
    public MaskingDialog(MaskingCommand maskingCommand, RhinoDoc doc) {
        Title = "SeaLion: Masking";
        ClientSize = new Size(600, 400);
        Resizable = true;
        
        this.maskingCommand = maskingCommand;
        this.doc = doc;

        Content = createDialogContent();
    }

    private DynamicLayout createDialogContent() {
        var headerPanel = Styling.createHeaderPanel(
            "Masking",
            "How do you want to mask away certain portions of the imported data?",
            true
        );
        var descriptionTextDynamicLayout = createDescriptionTextPanel();
        var objectsListDynamicLayout = createObjectsListPanel();
        var buttonPanel = createButtonPanel();

        var dynamicLayout = new DynamicLayout {
            BackgroundColor = Styling.colourDarker
        };
        dynamicLayout.BeginVertical();
        dynamicLayout.Add(headerPanel, true);
        dynamicLayout.Add(descriptionTextDynamicLayout, true);
        dynamicLayout.Add(objectsListDynamicLayout, true, true);
        dynamicLayout.Add(buttonPanel, true);
        dynamicLayout.EndVertical();

        return dynamicLayout;
    }

    private DynamicLayout createDescriptionTextPanel() {
        var longTextLabel = Styling.label(
            "Below is a list of the objects from which the masking will be performed. You can add or remove masking objects, and rename them to make them easier to manage.",
            9
        );
        var boldTextLabel = Styling.label(
            "Any changes made here will be reflected the next time any data is imported.",
            9, bold: true
        );

        var dynamicLayoutInner = new DynamicLayout {
            BackgroundColor = Styling.colourDark,
            Padding = 10
        };
        dynamicLayoutInner.BeginVertical();
        dynamicLayoutInner.Add(longTextLabel);
        dynamicLayoutInner.Add(boldTextLabel);
        dynamicLayoutInner.EndVertical();
        var dynamicLayout = new DynamicLayout {
            Padding = new Padding(20, 20, 20, 0)
        };
        dynamicLayout.BeginHorizontal();
        dynamicLayout.Add(dynamicLayoutInner);
        dynamicLayout.EndHorizontal();

        return dynamicLayout;
    }

    private void updateObjectsList() {
        var objectsDynamicLayout = new DynamicLayout {
            Padding = 15,
            Height = -1
        };
        objectsDynamicLayout.BeginVertical();
        
        if (this.objectPanels.Count == 0) {
            var emptyLabel = Styling.label(
                "No masking is currently being performed.",
                9, italic: true
            );
            objectsDynamicLayout.Add(emptyLabel);
        }

        foreach (var panelPair in this.objectPanels) {
            var panel = panelPair.Value;
            objectsDynamicLayout.Add(panel);
        }

        objectsDynamicLayout.EndVertical();

        var objectsScrollable = new Scrollable {
            BackgroundColor = Styling.colourDark,
            Border = BorderType.None,
            ExpandContentHeight = false,
            Content = objectsDynamicLayout
        };

        this.objectsPanel.Content = objectsScrollable;
    }

    private Panel createObjectsListPanel() {
        this.objectPanels = new Dictionary<Guid, Panel>();

        foreach (Guid maskingObject in this.maskingCommand.maskingObjects) {
            var objectPanel = createObjectPanel(maskingObject);
            this.objectPanels[maskingObject] = objectPanel;
        }

        this.objectsPanel = new Panel {
            Padding = new Padding(20, 20, 20, 0)
        };

        this.updateObjectsList();

        return this.objectsPanel;
    }

    private void highlightObject(Guid objectId) {
        /* At the moment this only has an effect once you close the window.
         * I haven't looked into how to make this happen while the window is still open. */
         
        if (this.highlightedObject != null) {
            this.doc.Objects.FindId((Guid) this.highlightedObject).Highlight(false);
        }
        this.doc.Objects.FindId(objectId).Highlight(true);
        this.highlightedObject = objectId;
    }

    private DynamicLayout createObjectPanel(Guid objectId) {
        var highlightButton = new Button {
            Text = "Highlight"
        };
        highlightButton.Click += (sender, e) => {
            this.highlightObject(objectId);
        };

        int index = maskingCommand.maskingObjects.IndexOf(objectId);
        string nameText = "No name";
        if (maskingCommand.maskingObjectNames[index] != "") {
            nameText = maskingCommand.maskingObjectNames[index];
        }

        var nameLabel = Styling.label(nameText, 10);
        var guidLabel = Styling.label(
            "(" + objectId.ToString() + ")",
            9, italic: true
        );
        var nameDynamicLayout = new DynamicLayout {
            Padding = new Padding(10, 0, 10, 0)
        };
        nameDynamicLayout.BeginVertical();
        nameDynamicLayout.Add(nameLabel);
        nameDynamicLayout.Add(guidLabel);
        nameDynamicLayout.EndVertical();

        var renameButton = new Button {
            Text = "Rename"
        };
        renameButton.Click += (sender, e) => {
            TextInputDialog textInputWindow = new TextInputDialog(
                "Rename",
                "Enter a new name for this object",
                nameText
            );
            string? result = textInputWindow.ShowModal(Rhino.UI.RhinoEtoApp.MainWindow);
            if (result == null) return;
            int index = maskingCommand.maskingObjects.IndexOf(objectId);
            maskingCommand.maskingObjectNames[index] = result;
            this.objectPanels[objectId] = createObjectPanel(objectId);
            this.updateObjectsList();
        };
        var deleteButton = new Button {
            Text = "Remove"
        };
        deleteButton.Click += (sender, e) => {
            maskingCommand.maskingObjects.Remove(objectId);
            this.objectPanels.Remove(objectId);
            this.updateObjectsList();
        };

        var dynamicLayout = new DynamicLayout {
            BackgroundColor = Styling.colourLight,
            Padding = 10
        };
        dynamicLayout.BeginHorizontal();
        dynamicLayout.Add(highlightButton, false);
        dynamicLayout.Add(nameDynamicLayout, true);
        dynamicLayout.Add(renameButton, false);
        dynamicLayout.Add(deleteButton, false);
        dynamicLayout.EndHorizontal();

        return dynamicLayout;
    }

    private DynamicLayout createButtonPanel() {
        var addButton = new Button{Text = "Add"};
        addButton.Click += (sender, e) => {
            Close(false);
            this.maskingCommand.promptUserSelection(this.doc);
        };

        var buttonPanel = new Panel {
            BackgroundColor = Styling.colourLight,
            Padding = 10,
            Content = addButton
        };

        var buttonDynamicLayout = new DynamicLayout {
            Padding = new Padding(0, 30, 0, 10)
        };
        buttonDynamicLayout.BeginHorizontal();
        buttonDynamicLayout.Add(null, true);
        buttonDynamicLayout.Add(buttonPanel);
        buttonDynamicLayout.Add(null, true);
        buttonDynamicLayout.EndHorizontal();

        return buttonDynamicLayout;
    }
}
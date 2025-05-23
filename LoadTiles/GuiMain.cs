using System;
using Rhino;
using Rhino.Commands;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Eto.Drawing;
using Eto.Forms;
using Rhino.UI;
using System.Collections.Generic;
using System.Text.Json;
using CesiumAuthentication;
using System.IO;
using System.Reflection;


namespace LoadTiles;

public class DialogResult {
    public string apiKey;
    public CesiumAsset selectedAsset;
    public double latitude;
    public double longitude;
    public double? altitude;
    public double radius;
    public DialogResult(string apiKey, CesiumAsset selectedAsset, double latitude, double longitude, double? altitude, double radius) {
        this.apiKey = apiKey;
        this.selectedAsset = selectedAsset;
        this.latitude = latitude;
        this.longitude = longitude;
        this.altitude = altitude;
        this.radius = radius;
    }
}

public class Coordinate {
    public double latitude, longitude;

    public Coordinate(double latitude, double longitude) {
        this.latitude = latitude;
        this.longitude = longitude;
    }
};

public class CoordinatePicker : Dialog<Coordinate>
{
    public CoordinatePicker()
    {
        Title = "SeaLion: Location picker";
        ClientSize = new Size(1024, 512);

        string html;
        var assembly = Assembly.GetExecutingAssembly();
        string resourceName = "LoadTiles.picker.html";
            
        using (Stream stream = assembly.GetManifestResourceStream(resourceName))
        using (StreamReader reader = new StreamReader(stream))
        {
            html = reader.ReadToEnd();
        }

        string tempPath = Path.Combine(Path.GetTempPath(), "picker.html");
        File.WriteAllText(tempPath, html);

        var webView = new WebView
        {
            Url = new Uri(tempPath),
            BackgroundColor = Colors.White
        };

       webView.DocumentTitleChanged += (s, e) =>
        {
            if (!e.Title.StartsWith("rhino://")) return;

            var uri = new Uri(e.Title);
            if (uri.Scheme == "rhino")
            {
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                string latitude = query["latitude"];
                string longitude = query["longitude"];

                RhinoApp.WriteLine($"Picked coordinates: Lat {latitude}, Lng {longitude}");
                Close(new Coordinate(Convert.ToDouble(latitude), Convert.ToDouble(longitude)));
            }
        };

        Content = webView;
        AbortButton = new Button{Text = "Cancel"};
        AbortButton.Click += (sender, e) => Close(null);
    }
}

public class LoadTilesGUI : Dialog<DialogResult> {
    private TextBox latitudeTextBox;
    private TextBox longitudeTextBox;
    private TextBox altitudeTextBox;
    private TextBox radiusTextBox;
    private Button authButton;

    private CesiumAsset selectedAsset;
    private Label selectedModelLabel;
    private static readonly HttpClient client = new HttpClient();

    private CesiumAsset getDefaultSelectedAsset() {
        return new CesiumAsset(
            2275207,
            "Google Photorealistic 3D Tiles",
            null, null, "", null, null, null, null, null, null
        );
    }

    public LoadTilesGUI() {
        Title = "SeaLion: Fetch data";
        ClientSize = new Size(400, 550);

        this.selectedAsset = this.getDefaultSelectedAsset();

        Content = createDialogContent();
    }

    private DynamicLayout createDialogContent() {
        var headerPanel = Styling.createHeaderPanel(
            "Fetch data",
            "What data do you want to import?",
            true
        );

        // Each DynamicLayout corresponds to one of the "cards" on the window
        var authDynamicLayout = createAuthenticationPanel();
        var modelDynamicLayout = createModelPanel();
        var positionDynamicLayout = createPositionPanel();
        var buttonsDynamicLayout = createButtonsPanel();

        var components = new List<Panel> {
            headerPanel,
            authDynamicLayout,
            modelDynamicLayout,
            positionDynamicLayout,
            buttonsDynamicLayout
        };
        return Styling.createDialogContent(components);
    }

    private DynamicLayout createAuthenticationPanel() {
        var authLabel = Styling.label("Authentication", 12);

        string loggedInText = "You are logged in.";

        var loggedInLabel = Styling.label(loggedInText, 10);
        var loggedInLabelPanel = new Panel {
            Padding = new Padding(0, 0, 30, 0),
            Content = loggedInLabel
        };

        this.authButton = new Button{Text = "Log out"};
        this.authButton.Click += (sender, e) => {
            // Log out the user
            AuthSession.Logout();
            MessageBox.Show("Logged out successfully!");
            Close(null);
        };

        // Logged in status text and button
        var loggedInStatusDynamicLayout = new DynamicLayout {
            Padding = new Padding(0, 15, 0, 0)
        };
        loggedInStatusDynamicLayout.BeginHorizontal();
        loggedInStatusDynamicLayout.Add(loggedInLabelPanel);
        loggedInStatusDynamicLayout.Add(null, true);
        loggedInStatusDynamicLayout.Add(this.authButton);
        loggedInStatusDynamicLayout.EndHorizontal();
        
        // Container for whole card
        var authDynamicLayoutInner = new DynamicLayout {
            BackgroundColor = Styling.colourDark,
            Padding = 10
        };
        authDynamicLayoutInner.BeginVertical();
        authDynamicLayoutInner.Add(authLabel);
        authDynamicLayoutInner.Add(loggedInStatusDynamicLayout);
        authDynamicLayoutInner.EndVertical();
        var authDynamicLayout = new DynamicLayout {
            Padding = new Padding(20, 20, 20, 0)
        };
        authDynamicLayout.BeginHorizontal();
        authDynamicLayout.Add(authDynamicLayoutInner);
        authDynamicLayout.EndHorizontal();

        return authDynamicLayout;
    }

    private DynamicLayout createModelPanel() {
        var modelLabel = Styling.label("Model", 12);

        this.selectedModelLabel = Styling.label(this.selectedAsset.name, 10, bold: true);
        var selectedModelLabelPanel = new Panel {
            Padding = new Padding(0, 0, 20, 0),
            Content = this.selectedModelLabel
        };

        var changeModelButton = new Button{Text = "Change"};
        changeModelButton.Click += (sender, e) => {
            this.selectNewModel();
        };

        // Current model name label and "Change" button
        var currentModelDynamicLayout = new DynamicLayout {
            Padding = new Padding(0, 15, 0, 0)
        };
        currentModelDynamicLayout.BeginHorizontal();
        currentModelDynamicLayout.Add(selectedModelLabelPanel);
        currentModelDynamicLayout.Add(null, true);
        currentModelDynamicLayout.Add(changeModelButton);
        currentModelDynamicLayout.EndHorizontal();

        // Container for whole card
        var modelDynamicLayoutInner = new DynamicLayout {
            BackgroundColor = Styling.colourDark,
            Padding = 10
        };
        modelDynamicLayoutInner.BeginVertical();
        modelDynamicLayoutInner.Add(modelLabel);
        modelDynamicLayoutInner.Add(currentModelDynamicLayout);
        modelDynamicLayoutInner.EndVertical();
        var modelDynamicLayout = new DynamicLayout {
            Padding = new Padding(20, 20, 20, 0)
        };
        modelDynamicLayout.BeginHorizontal();
        modelDynamicLayout.Add(modelDynamicLayoutInner);
        modelDynamicLayout.EndHorizontal();

        return modelDynamicLayout;
    }

    private DynamicLayout createPositionPanel() {
        var positionLabel = Styling.label("Position", 12);

        var coordinatePickerButton = new Button{Text = "  Select from map  "};
        coordinatePickerButton.Click += (sender, e) => {
            var window = new CoordinatePicker();
            Coordinate coords = window.ShowModal(Rhino.UI.RhinoEtoApp.MainWindow);
            if (coords != null) {
                latitudeTextBox.Text = coords.latitude.ToString();
                longitudeTextBox.Text = coords.longitude.ToString();
            }
        };

        var positionLabelDynamicLayout = new DynamicLayout();
        positionLabelDynamicLayout.BeginHorizontal();
        positionLabelDynamicLayout.Add(positionLabel);
        positionLabelDynamicLayout.Add(null, true);
        positionLabelDynamicLayout.Add(coordinatePickerButton);
        positionLabelDynamicLayout.EndHorizontal();

        var latitudeLabel = Styling.label("Latitude", 10);
        this.latitudeTextBox = new TextBox();
        var latitudeDynamicLayout = new DynamicLayout();
        latitudeDynamicLayout.BeginVertical();
        latitudeDynamicLayout.Add(latitudeLabel);
        latitudeDynamicLayout.Add(this.latitudeTextBox, true);
        latitudeDynamicLayout.EndVertical();

        var longitudeLabel = Styling.label("Longitude", 10);
        this.longitudeTextBox = new TextBox();
        var longitudeDynamicLayout = new DynamicLayout();
        longitudeDynamicLayout.BeginVertical();
        longitudeDynamicLayout.Add(longitudeLabel);
        longitudeDynamicLayout.Add(this.longitudeTextBox, true);
        longitudeDynamicLayout.EndVertical();

        var altitudeLabel = Styling.label("Altitude", 10);
        this.altitudeTextBox = new TextBox();
        var altitudeTextBoxLabel = Styling.label("  metres", 9);
        var altitudeTextBoxDynamicLayout = new DynamicLayout();
        altitudeTextBoxDynamicLayout.BeginHorizontal();
        altitudeTextBoxDynamicLayout.Add(this.altitudeTextBox, true);
        altitudeTextBoxDynamicLayout.Add(altitudeTextBoxLabel);
        altitudeTextBoxDynamicLayout.EndHorizontal();
        var altitudeDynamicLayout = new DynamicLayout();
        altitudeDynamicLayout.BeginVertical();
        altitudeDynamicLayout.Add(altitudeLabel);
        altitudeDynamicLayout.Add(altitudeTextBoxDynamicLayout, true);
        altitudeDynamicLayout.EndVertical();

        var radiusLabel = Styling.label("Radius", 10);
        this.radiusTextBox = new TextBox();
        this.radiusTextBox.Text = "200";
        var radiusTextBoxLabel = Styling.label("  metres", 9);
        var radiusTextBoxDynamicLayout = new DynamicLayout();
        radiusTextBoxDynamicLayout.BeginHorizontal();
        radiusTextBoxDynamicLayout.Add(this.radiusTextBox, true);
        radiusTextBoxDynamicLayout.Add(radiusTextBoxLabel);
        radiusTextBoxDynamicLayout.EndHorizontal();
        var radiusDynamicLayout = new DynamicLayout();
        radiusDynamicLayout.BeginVertical();
        radiusDynamicLayout.Add(radiusLabel);
        radiusDynamicLayout.Add(radiusTextBoxDynamicLayout, true);
        radiusDynamicLayout.EndVertical();

        // 2x2 grid
        TableLayout positionTableLayout = new TableLayout {
            Spacing = new Size(20, 20),
            Padding = new Padding(0, 20, 0, 0),
            Rows = {
                new TableRow(latitudeDynamicLayout, longitudeDynamicLayout),
                new TableRow(altitudeDynamicLayout, radiusDynamicLayout)
            }
        };

        // Container for whole card
        var positionDynamicLayoutInner = new DynamicLayout {
            BackgroundColor = Styling.colourDark,
            Padding = 10
        };
        positionDynamicLayoutInner.BeginVertical();
        positionDynamicLayoutInner.Add(positionLabelDynamicLayout);
        positionDynamicLayoutInner.Add(positionTableLayout, true);
        positionDynamicLayoutInner.EndVertical();
        var positionDynamicLayout = new DynamicLayout {
            Padding = new Padding(20, 20, 20, 0)
        };
        positionDynamicLayout.BeginHorizontal();
        positionDynamicLayout.Add(positionDynamicLayoutInner);
        positionDynamicLayout.EndHorizontal();

        return positionDynamicLayout;
    }

    private DynamicLayout createButtonsPanel() {
        DefaultButton = new Button{Text = "Import"};
        DefaultButton.Click += (sender, e) => {
            DialogResult result = this.getUserInput();
            if (result == null) return;
            Close(result);
        };
        var defaultButtonPanel = new Panel {
            Padding = new Padding(0, 0, 20, 0),
            Content = DefaultButton
        };

        AbortButton = new Button{Text = "Cancel"};
        AbortButton.Click += (sender, e) => Close(null);

        StackLayout buttonsStackLayoutInner = new StackLayout {
            Orientation = Orientation.Horizontal,
            BackgroundColor = Styling.colourLight,
            Padding = 10,
            Items = { defaultButtonPanel, AbortButton }
        };
        var buttonsDynamicLayout = new DynamicLayout {
            Padding = new Padding(20, 30, 20, 0)
        };
        buttonsDynamicLayout.BeginHorizontal();
        buttonsDynamicLayout.Add(null, true);
        buttonsDynamicLayout.Add(buttonsStackLayoutInner);
        buttonsDynamicLayout.Add(null, true);
        buttonsDynamicLayout.EndHorizontal();

        return buttonsDynamicLayout;
    }

    private void selectNewModel() {
        if (!AuthSession.IsLoggedIn) {
            return;
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AuthSession.CesiumAccessToken);
        string json = Task.Run(() => client.GetStringAsync("https://api.cesium.com/v1/assets?type=3DTILES").Result).GetAwaiter().GetResult();
        List<CesiumAsset> assets = CesiumAssets.FromJson(json);

        CesiumImportDialog dialog = new CesiumImportDialog(assets);
        CesiumAsset asset = dialog.ShowModal(Rhino.UI.RhinoEtoApp.MainWindow);

        if (asset != null) {
            this.selectedAsset = asset;
            this.selectedModelLabel.Text = asset.name;
        }
    }

    public void prefillData(double latitude, double longitude, double? altitude, double radius, CesiumAsset selectedAsset) {
        // Called if we detect saved data in the currently loaded project
        this.latitudeTextBox.Text = latitude.ToString();
        this.longitudeTextBox.Text = longitude.ToString();
        this.altitudeTextBox.Text = altitude != null ? altitude.ToString() : "";
        this.radiusTextBox.Text = radius.ToString();
        this.selectedAsset = selectedAsset;
        this.selectedModelLabel.Text = selectedAsset.name;
    }

    private DialogResult getUserInput() {
        string apiKey = AuthSession.CesiumAccessToken;
        string latitudeText = this.latitudeTextBox.Text;
        string longitudeText = this.longitudeTextBox.Text;
        string altitudeText = this.altitudeTextBox.Text;
        string radiusText = this.radiusTextBox.Text;
        bool emptyAltitude = string.IsNullOrWhiteSpace(altitudeText);
        bool canConvertLatitude = double.TryParse(latitudeText, out double latitude);
        bool canConvertLongitude = double.TryParse(longitudeText, out double longitude);
        bool canConvertAltitude = double.TryParse(altitudeText, out double inputAltitude);
        bool canConvertRadius = double.TryParse(radiusText, out double radius);
        bool latitudeValid = canConvertLatitude && latitude >= -90 && latitude <= 90;
        bool longitudeValid = canConvertLongitude && longitude >= -180 && longitude <= 180;
        double? altitude = emptyAltitude ? null : inputAltitude;
        if (string.IsNullOrWhiteSpace(apiKey)) {
            MessageBox.Show("You are not logged in!", "Error", MessageBoxType.Error);
            return null;
        } 

        if (!latitudeValid || !longitudeValid) {
            MessageBox.Show("You have entered invalid coordinate values.", "Error", MessageBoxType.Error);
            if (!latitudeValid) {
                this.latitudeTextBox.BackgroundColor = Colors.DarkRed;
                this.latitudeTextBox.TextColor = Colors.White;
            } else {
                this.latitudeTextBox.BackgroundColor = Colors.White;
                this.latitudeTextBox.TextColor = Colors.Black;
            }
            if (!longitudeValid) {
                this.longitudeTextBox.BackgroundColor = Colors.DarkRed;
                this.longitudeTextBox.TextColor = Colors.White;
            } else {
                this.longitudeTextBox.BackgroundColor = Colors.White;
                this.longitudeTextBox.TextColor = Colors.Black;
            }
            return null;
        } else {
            this.latitudeTextBox.BackgroundColor = Colors.White;
            this.longitudeTextBox.BackgroundColor = Colors.White;
            this.latitudeTextBox.TextColor = Colors.Black;
            this.longitudeTextBox.TextColor = Colors.Black;
        }

        if (!canConvertAltitude && !emptyAltitude) {
            MessageBox.Show("You have entered an invalid altitude value.", "Error", MessageBoxType.Error);
            this.altitudeTextBox.BackgroundColor = Colors.DarkRed;
            this.altitudeTextBox.TextColor = Colors.White;
        } else {
            this.altitudeTextBox.BackgroundColor = Colors.White;
            this.altitudeTextBox.TextColor = Colors.Black;
        }

        if (!canConvertRadius) {
            MessageBox.Show("You have entered an invalid radius value.", "Error", MessageBoxType.Error);
            this.radiusTextBox.BackgroundColor = Colors.DarkRed;
            this.radiusTextBox.TextColor = Colors.White;
        } else {
            this.radiusTextBox.BackgroundColor = Colors.White;
            this.radiusTextBox.TextColor = Colors.Black;
        }

        RhinoApp.WriteLine("Fetch Successful! \n" +
            "Model Name: " + this.selectedAsset.name + "\n" +
            "Latitude: " + latitude.ToString() + "\n" +
            "Longitude: " + longitude.ToString() + "\n" +
            "Altitude: " + (altitude == null ? "(Not specified)" : altitude.ToString()) + "\n" +
            "Radius: " + radius.ToString()
        );

        return new DialogResult(
            apiKey,
            this.selectedAsset,
            latitude,
            longitude,
            altitude,
            radius
        );
    }
}
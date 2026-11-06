using Axiom.Harness.Application;
using Axiom.Harness.Domain;

namespace Axiom.Harness.WinForms;

public sealed class MainForm : Form
{
    private readonly IModelClient _models;
    private readonly IAgentRunner _runner;
    private readonly IAgentRunRepository _repository;
    private readonly ComboBox _model = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top };
    private readonly TextBox _roots = new() { PlaceholderText = "Carpetas permitidas (separadas por ;)" , Dock = DockStyle.Top };
    private readonly TextBox _objective = new() { Multiline = true, Height = 90, Dock = DockStyle.Top, PlaceholderText = "Objetivo para el agente..." };
    private readonly Button _image = new() { Text = "Adjuntar imagen", Dock = DockStyle.Top };
    private readonly Label _imagePath = new() { Text = "Sin imagen adjunta", Dock = DockStyle.Top, AutoEllipsis = true };
    private readonly TextBox _chat = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
    private readonly TextBox _trace = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
    private readonly Button _run = new() { Text = "Ejecutar agente", Dock = DockStyle.Top };
    private readonly Button _cancel = new() { Text = "Cancelar", Dock = DockStyle.Top, Enabled = false };
    private readonly ListBox _history = new() { Dock = DockStyle.Left, Width = 300 };
    private readonly TextBox _details = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
    private CancellationTokenSource? _runCancellation;

    public MainForm(IModelClient models, IAgentRunner runner, IAgentRunRepository repository)
    {
        _models = models; _runner = runner; _repository = repository;
        Text = "Axiom Harness IA"; Width = 1250; Height = 800; StartPosition = FormStartPosition.CenterScreen;
        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(CreateChatPage()); tabs.TabPages.Add(CreateHistoryPage());
        Controls.Add(tabs); Load += async (_, _) => await LoadModelsAsync();
    }
    private TabPage CreateChatPage()
    {
        var page = new TabPage("Chat y ejecución");
        var controls = new Panel { Dock = DockStyle.Top, Height = 255, Padding = new Padding(8) };
        controls.Controls.Add(_cancel); controls.Controls.Add(_run); controls.Controls.Add(_objective); controls.Controls.Add(_imagePath); controls.Controls.Add(_image); controls.Controls.Add(_roots); controls.Controls.Add(_model);
        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 700 };
        split.Panel1.Controls.Add(_chat); split.Panel2.Controls.Add(_trace);
        page.Controls.Add(split); page.Controls.Add(controls);
        _run.Click += async (_, _) => await RunAsync(); _cancel.Click += (_, _) => _runCancellation?.Cancel();
        _image.Click += (_, _) => SelectImage();
        return page;
    }
    private TabPage CreateHistoryPage()
    {
        var page = new TabPage("Ejecuciones"); var refresh = new Button { Text = "Actualizar", Dock = DockStyle.Top };
        page.Controls.Add(_details); page.Controls.Add(_history); page.Controls.Add(refresh);
        refresh.Click += async (_, _) => await LoadHistoryAsync();
        _history.SelectedIndexChanged += async (_, _) => await ShowSelectedAsync();
        return page;
    }
    private async Task LoadModelsAsync()
    {
        try
        {
            var models = await _models.ListModelsAsync(CancellationToken.None); _model.Items.AddRange(models.Cast<object>().ToArray());
            _model.SelectedItem = models.FirstOrDefault(x => x.Equals("qwen3.5:4b-q4_K_M", StringComparison.Ordinal)) ?? models.FirstOrDefault();
            if (_model.SelectedIndex < 0) throw new InvalidOperationException("Ollama no informó modelos instalados.");
        }
        catch (Exception ex) { MessageBox.Show($"No se pudo conectar con Ollama en localhost:11434.\n\n{ex.Message}", "Axiom Harness", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
    private async Task RunAsync()
    {
        if (_model.SelectedItem is not string model || string.IsNullOrWhiteSpace(_objective.Text)) { MessageBox.Show("Seleccione un modelo y escriba un objetivo."); return; }
        _chat.Clear(); _trace.Clear(); _run.Enabled = false; _cancel.Enabled = true; _runCancellation = new CancellationTokenSource();
        var roots = _roots.Text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(Directory.Exists).ToList();
        var progress = new Progress<AgentProgress>(item =>
        {
            if (item.Kind == AgentStepKind.ModelRequest && item.Name == "stream") _chat.AppendText(item.Content);
            else _trace.AppendText($"[{item.Kind}] {item.Name}: {item.Content}{Environment.NewLine}");
        });
        try
        {
            var imagePath = _imagePath.Tag as string;
            var run = await _runner.RunAsync(new AgentRunRequest(model, _objective.Text, imagePath, roots), progress, _runCancellation.Token);
            _trace.AppendText($"{Environment.NewLine}Estado final: {run.Status}{Environment.NewLine}");
        }
        finally { _run.Enabled = true; _cancel.Enabled = false; _runCancellation.Dispose(); _runCancellation = null; }
    }
    private void SelectImage()
    {
        using var dialog = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.webp|All files|*.*", Title = "Seleccione una imagen" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _imagePath.Tag = dialog.FileName; _imagePath.Text = dialog.FileName;
    }
    private async Task LoadHistoryAsync()
    {
        var runs = await _repository.ListAsync(CancellationToken.None); _history.DataSource = runs.ToList(); _history.DisplayMember = "Objective";
    }
    private async Task ShowSelectedAsync()
    {
        if (_history.SelectedItem is not AgentRun selected) return;
        var run = await _repository.GetAsync(selected.Id, CancellationToken.None); if (run is null) return;
        _details.Text = $"{run.Status} | {run.Model} | {run.StartedAtUtc:u}{Environment.NewLine}{run.Objective}{Environment.NewLine}{Environment.NewLine}" + string.Join(Environment.NewLine, run.Steps.OrderBy(x => x.Sequence).Select(x => $"[{x.Kind}] {x.Name}: {x.Payload}"));
    }
}

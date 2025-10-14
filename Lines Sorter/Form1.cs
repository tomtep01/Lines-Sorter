using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Lines_Sorter
{
    public partial class Form1 : Form
    {
        private System.Threading.CancellationTokenSource _cancellationTokenSource;
        private int _processedFilesCounter;
        private long _keptLinesCounter;
        private int _totalFilesToProcess;
        public Form1()
        {
            InitializeComponent();
            this.listBox1.AllowDrop = true;
        }

        private void listBox1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        private void listBox1_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            // Add each file path to the list box.
            foreach (string file in files)
            {
                listBox1.Items.Add(file);
            }
        }
        private List<string> RunSingleThreadedSearch(
     IEnumerable<object> files,
     string[] searchTerms,
     StringComparison comparisonMode,
     bool isExcludeMode,
     CancellationToken token)
        {
            var linesToKeep = new List<string>();
            long keptLinesCount = 0;
            int processedFiles = 0;
            int totalFiles = files.Count();

            foreach (var item in files)
            {
                if (token.IsCancellationRequested) break;

                processedFiles++;
                string filePath = item.ToString();
                string fileName = System.IO.Path.GetFileName(filePath);
                string displayName = fileName.Length > 20 ? fileName.Substring(0, 20) + "..." : fileName;

                this.Invoke((Action)delegate {
                    lblFileCount.Text = $"Processing file {processedFiles} of {totalFiles}";
                    progressBar1.Value = 0;
                });

                try
                {
                    if (!System.IO.File.Exists(filePath))
                    {
                        this.Invoke((Action)delegate { lblStatus.Text = $"File not found: {displayName}"; });
                        continue;
                    }

                    var fileInfo = new System.IO.FileInfo(filePath);
                    long totalBytes = fileInfo.Length;

                    using (var fs = new System.IO.FileStream(filePath, FileMode.Open, FileAccess.Read))
                    using (var sr = new System.IO.StreamReader(fs))
                    {
                        string line;
                        long linesProcessedThisFile = 0;
                        long lastUpdateLineCount = 0;
                        var lastUpdateTime = DateTime.UtcNow;
                        const double updateIntervalSeconds = 1.0;
                        long estimatedTotalLines = 0;

                        while ((line = sr.ReadLine()) != null)
                        {
                            if (token.IsCancellationRequested) break;
                            linesProcessedThisFile++;

                            bool matchFound = false;
                            foreach (var term in searchTerms)
                            {
                                if (line.IndexOf(term, comparisonMode) >= 0)
                                {
                                    matchFound = true;
                                    break;
                                }
                            }

                            if (isExcludeMode != matchFound)
                            {
                                keptLinesCount++;
                                linesToKeep.Add(line);
                            }

                            var elapsedSeconds = (DateTime.UtcNow - lastUpdateTime).TotalSeconds;
                            if (elapsedSeconds >= updateIntervalSeconds)
                            {
                                if (totalBytes > 0)
                                {
                                    double percentRead = (double)fs.Position / totalBytes;
                                    if (percentRead > 0.001)
                                        estimatedTotalLines = (long)(linesProcessedThisFile / percentRead);
                                }

                                var linesSinceLastUpdate = linesProcessedThisFile - lastUpdateLineCount;
                                var linesPerSecond = linesSinceLastUpdate / elapsedSeconds;
                                int percentage = (totalBytes > 0) ? (int)((double)fs.Position * 100 / totalBytes) : 0;
                                string lineCountText = (estimatedTotalLines > 0)
                                    ? $"Line: {linesProcessedThisFile:N0} of ~{estimatedTotalLines:N0}"
                                    : $"Line: {linesProcessedThisFile:N0}";

                                this.Invoke((Action)delegate {
                                    progressBar1.Value = percentage;
                                    lblStatus.Text = $"Processing: {displayName}... {percentage}%";
                                    label3.Text = lineCountText;
                                    label4.Text = $"Speed: {linesPerSecond:N0} lines/sec";
                                    label5.Text = $"Found: {keptLinesCount:N0}";
                                });

                                lastUpdateTime = DateTime.UtcNow;
                                lastUpdateLineCount = linesProcessedThisFile;
                            }
                        }
                        this.Invoke((Action)delegate {
                            progressBar1.Value = 100;
                            lblStatus.Text = $"Finished: {displayName}";
                            label3.Text = $"Line: {linesProcessedThisFile:N0} of {linesProcessedThisFile:N0}";
                            label4.Text = "";
                            label5.Text = $"Kept: {keptLinesCount:N0}";
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error on file {filePath}: {ex.Message}");
                }
            }
            return linesToKeep;
        }

        private List<string> RunMultiThreadedSearch(
      IEnumerable<object> files,
    string[] searchTerms,
    StringComparison comparisonMode,
    bool isExcludeMode,
    CancellationToken token)
        {
            var linesToKeep = new System.Collections.Concurrent.ConcurrentBag<string>();

            try
            {
                var parallelOptions = new ParallelOptions { CancellationToken = token };

                Parallel.ForEach(files, parallelOptions, (item, loopState) =>
                {
                    token.ThrowIfCancellationRequested();

                    try
                    {
                        string filePath = item.ToString();
                        if (!System.IO.File.Exists(filePath)) return;

                        using (var sr = new System.IO.StreamReader(filePath))
                        {
                            string line;
                            long lineCounter = 0;
                            while ((line = sr.ReadLine()) != null)
                            {
                                if (lineCounter++ % 2048 == 0) token.ThrowIfCancellationRequested();

                                bool matchFound = false;
                                foreach (var term in searchTerms)
                                {
                                    if (line.IndexOf(term, comparisonMode) >= 0)
                                    {
                                        matchFound = true;
                                        break;
                                    }
                                }

                                if (isExcludeMode != matchFound)
                                {
                                    linesToKeep.Add(line);
                                    System.Threading.Interlocked.Increment(ref _keptLinesCounter);
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error on a file (parallel): {ex.Message}");
                    }
                    finally
                    {

                        System.Threading.Interlocked.Increment(ref _processedFilesCounter);
                    }
                });
            }
            catch (OperationCanceledException) {  }

            return linesToKeep.ToList();
        }
        private void button1_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            // Allow the user to select multiple files
            openFileDialog.Multiselect = true;
            // Set the filter to show all file types
            openFileDialog.Filter = "All files (*.*)|*.*";

            // Show the dialog and if the user clicks OK, add the selected file paths to the list box
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                foreach (string file in openFileDialog.FileNames)
                {
                    listBox1.Items.Add(file);
                }
            }
        }
        private void button3_Click(object sender, EventArgs e)
        {
            // Check if any items are selected in the list box.
            if (listBox1.SelectedItems.Count > 0)
            {
                // Create a temporary list to hold the items to be removed to avoid issues with modifying the collection while iterating over it.
                var selectedItems = new System.Collections.ArrayList(listBox1.SelectedItems);
                // Remove each selected item from the list box.
                foreach (var item in selectedItems)
                {
                    listBox1.Items.Remove(item);
                }
            }
            else
            {
                // If no item is selected, show a message to the user.
                MessageBox.Show("Please select at least one file to remove.", "No File Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        private async void button2_Click(object sender, EventArgs e)
        {
            if (listBox1.Items.Count == 0) { MessageBox.Show("Please add file paths."); return; }
            string searchText = textBox1.Text;
            if (string.IsNullOrWhiteSpace(searchText)) { MessageBox.Show("Please enter search text."); return; }

            string[] searchTerms = searchText.Split('|').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToArray();
            if (searchTerms.Length == 0) { MessageBox.Show("Invalid search text."); return; }

            var comparisonMode = checkBox1.Checked ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            bool isExcludeMode = checkBox2.Checked;
            bool useMultiThreading = multiThreadCheckBox.Checked;

            _cancellationTokenSource = new System.Threading.CancellationTokenSource();
            var token = _cancellationTokenSource.Token;
            var filesToProcess = listBox1.Items.Cast<object>().ToList();
            List<string> finalResults = new List<string>();


            _totalFilesToProcess = filesToProcess.Count;
            _processedFilesCounter = 0;
            _keptLinesCounter = 0;

            SetUiStateForProcessing(true);
            ConfigureUiForMode(useMultiThreading);
            label5.Text = "";
            if (useMultiThreading)
            {
                timer1.Start();
            }

            try
            {
                if (useMultiThreading)
                {
                    finalResults = await Task.Run(() =>
                        RunMultiThreadedSearch(filesToProcess, searchTerms, comparisonMode, isExcludeMode, token), token)
                        .ConfigureAwait(false);
                }
                else
                {

                    finalResults = await Task.Run(() =>
                        RunSingleThreadedSearch(filesToProcess, searchTerms, comparisonMode, isExcludeMode, token), token)
                        .ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                finalResults = new List<string>();
            }
            finally
            {
                if (useMultiThreading)
                {
                    timer1.Stop();
                }
                this.Invoke((Action)delegate {
                    SetUiStateForProcessing(false);
                    ConfigureUiForMode(false);
                });
            }

        
            this.Invoke((Action)delegate {
                label5.Text = $"Found: {finalResults.Count:N0}";

                if (token.IsCancellationRequested)
                {
                    lblStatus.Text = "Operation Cancelled.";
                    if (finalResults.Count > 0)
                    {
                        var result = MessageBox.Show("Save partial results?", "Save Partial Results?", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (result == DialogResult.Yes) SaveResults(finalResults);
                    }
                }
                else
                {
                    lblFileCount.Text = $"Processed {filesToProcess.Count} of {filesToProcess.Count} files.";
                    lblStatus.Text = "Processing complete.";
                    if (finalResults.Count > 0)
                    {
                        SaveResults(finalResults);
                    }
                    else
                    {
                        MessageBox.Show("No lines to keep based on the specified criteria.", "No Results", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            });
        }
        private void ConfigureUiForMode(bool isMultiThreaded)
        {
            if (isMultiThreaded)
            {
                label3.Visible = false;
                label4.Visible = false;
                lblStatus.Visible = false;
                progressBar1.Style = ProgressBarStyle.Marquee;
            }
            else
            {
                label3.Visible = true;
                label4.Visible = true;
                lblStatus.Visible = true;
                progressBar1.Style = ProgressBarStyle.Blocks;
            }
        }

        private void SetUiStateForProcessing(bool isProcessing)
        {
            button2.Enabled = !isProcessing;
            button4.Enabled = isProcessing;
            listBox1.Enabled = !isProcessing;
            textBox1.Enabled = !isProcessing;
            checkBox1.Enabled = !isProcessing;
            checkBox2.Enabled = !isProcessing;
            multiThreadCheckBox.Enabled = !isProcessing; 
        }


        private void SaveResults(System.Collections.Generic.List<string> lines)
        {
            using (var saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "Text Files (*.txt)|*.txt|All files (*.*)|*.*";
                saveFileDialog.Title = "Save the processed file";
                saveFileDialog.FileName = "filtered_output.txt";

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        System.IO.File.WriteAllLines(saveFileDialog.FileName, lines);
                        MessageBox.Show("File saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        lblStatus.Text = "File saved successfully.";
                    }
                    catch (System.Exception ex)
                    {
                        MessageBox.Show($"An error occurred while saving the file:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        lblStatus.Text = "Error saving file.";
                    }
                }
                else
                {
                    lblStatus.Text = "Save operation cancelled.";
                }
            }
        }

        private void saveFileDialog1_FileOk(object sender, CancelEventArgs e)
        {

        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                button4.Enabled = false; // Disable the button to prevent multiple clicks.
            }
        }

        private void lblStatus_Click(object sender, EventArgs e)
        {

        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            lblFileCount.Text = $"Processed {_processedFilesCounter} of {_totalFilesToProcess} files.";
            label5.Text = $"Found: {System.Threading.Interlocked.Read(ref _keptLinesCounter):N0}";
        }
    }
}

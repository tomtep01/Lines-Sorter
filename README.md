# Lines Sorter - A Highly Optimized File Filtering Tool

Lines Sorter is a high-performance Windows Forms application designed for efficiently filtering lines from large text files. It provides a user-friendly interface to specify multiple search criteria and processes files swiftly, even those with millions of lines. This tool is ideal for developers, data analysts, and anyone who needs to quickly extract relevant information from large text-based data sets.

The application is built with a focus on performance, utilizing asynchronous processing to keep the UI responsive and providing real-time feedback on the filtering progress.

## Features

*   **Multiple File Handling:** Add files to be processed using a convenient drag-and-drop interface or a standard file selection dialog.
*   **Advanced Search Capabilities:**
    *   Filter lines based on multiple search terms simultaneously, separated by a `|` (pipe) character.
    *   Option for case-sensitive or case-insensitive matching.
*   **High-Performance Processing:**
    *   Employs efficient `StreamReader` to process large files line-by-line without consuming excessive memory. [1, 2, 4]
    *   Asynchronous processing prevents the user interface from freezing during intensive file operations.
    *   Real-time progress updates, including:
        *   Overall progress for the current file.
        *   The number of lines processed and an estimated total number of lines.
        *   Processing speed measured in lines per second.
*   **User-Friendly Interface:**
    *   Intuitive controls for adding, removing, and processing files.
    *   Clear status labels provide feedback on the current operation.
    *   A progress bar offers a visual representation of the current file's progress.
*   **Cancellation and Partial Results:**
    *   Cancel an ongoing filtering process at any time.
    *   Option to save the lines that have been found so far when a process is canceled.
*   **Saving Results:** Save the filtered lines to a new text file.

## How to Use

1.  **Add Files:**
    *   Drag and drop one or more files from your computer onto the list box in the application.
    *   Alternatively, click the "Add Files" button to open a file dialog and select the files you want to process.

2.  **Specify Search Terms:**
    *   In the "Search Text" input field, enter the text you want to search for.
    *   To search for multiple terms, separate them with a pipe (`|`) character (e.g., `error|warning|critical`).

3.  **Set Search Options:**
    *   Check the "Case Sensitive" box if you want the search to match the exact casing of your search terms. Leave it unchecked for a case-insensitive search.

4.  **Start Processing:**
    *   Click the "Start Processing" button to begin filtering the lines from the added files.

5.  **Monitor Progress:**
    *   The application will display the progress of the operation, including the current file being processed, the number of lines scanned, and the processing speed.

6.  **Cancel (Optional):**
    *   If you need to stop the process, click the "Cancel" button. You will be prompted to save any results that have been found up to that point.

7.  **Save the Output:**
    *   Once the processing is complete (or if you chose to save partial results after canceling), a "Save File" dialog will appear. Choose a location and a name for the file that will contain the filtered lines.

## Building from Source

To build this application from the provided C# source code, you will need:

*   **Prerequisites:**
    *   Windows Operating System
    *   .NET Framework (the version can be determined from the project files, but a recent version should be compatible)
    *   Microsoft Visual Studio

*   **Steps:**
    1.  Clone or download the repository containing the source code.
    2.  Open the solution file (`.sln`) in Visual Studio.
    3.  Build the solution (usually by pressing `F6` or selecting `Build > Build Solution` from the menu).
    4.  The executable file will be located in the `bin/Debug` or `bin/Release` folder within the project directory.

# 2AFC Data Plotter

This script plots 2AFC (Two Alternative Forced Choice) experiment results from multiple datasets.

## Features

- **Multiple Dataset Support**: Plot data from Bricks004, Bricks101, and Rock062 datasets
- **Flexible Selection**: Choose which datasets to plot individually or all together
- **Recent Files Filter**: Select only the most recent N files per dataset
- **Side-by-side Comparison**: When plotting multiple datasets, they are displayed in a 1x3 format
- **Interactive and Command Line Modes**: Use either interactive prompts or command line arguments

## Usage

### Command Line Mode

```bash
# Plot all datasets with all files
python plot.py

# Plot specific datasets
python plot.py --datasets bricks004 bricks101

# Plot recent 3 files from all datasets
python plot.py --recent 3

# Plot recent 5 files from specific datasets
python plot.py --datasets rock062 --recent 5

# Show help
python plot.py --help
```

### Interactive Mode

Run the script without arguments to enter interactive mode:

```bash
python plot.py
```

You'll be prompted to:
1. Select which datasets to plot (1-5 options)
2. Choose how many recent files to include

### Dataset Options

- **bricks004**: Bricks004_2AFC results
- **bricks101**: Bricks101_2AFC results  
- **rock062**: Rock062_2AFC results

### Command Line Arguments

- `--datasets` or `-d`: Specify which datasets to plot (choices: bricks004, bricks101, rock062)
- `--recent` or `-r`: Number of recent files to plot per dataset (default: all files)

## Examples

```bash
# Default: Plot all datasets with all files in 1x3 format
python plot.py

# Plot only Bricks004 dataset
python plot.py --datasets bricks004

# Plot recent 3 files from Bricks004 and Rock062
python plot.py --datasets bricks004 rock062 --recent 3

# Plot recent 5 files from all datasets
python plot.py --recent 5
```

## Output

- **Single Dataset**: Full window plot with session lines and average
- **Multiple Datasets**: Side-by-side subplots (1x3 format)
- **Color Coding**: Each dataset has its own color scheme (Blue for Bricks004, Red for Bricks101, Green for Rock062)
- **Session Information**: Each line represents a different session with timestamp
- **Average Line**: Black dashed line showing average across all sessions
- **Summary Statistics**: Text box with session count, trial count, and date range

## Data Summary

After plotting, the script prints a detailed summary including:
- Number of files processed per dataset
- Total trials per dataset
- Individual file information with timestamps
- Overall summary statistics

## Requirements

- Python 3.7+
- pandas
- matplotlib
- numpy
- seaborn

## Installation

```bash
pip install pandas matplotlib numpy seaborn
```
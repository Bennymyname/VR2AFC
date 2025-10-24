import pandas as pd
import matplotlib.pyplot as plt
import numpy as np
import glob
import os
from datetime import datetime
import seaborn as sns
import argparse

# Define the available datasets
DATASETS = {
    'bricks004': 'Bricks004_results',
    'bricks101': 'Bricks101_results', 
    'rock062': 'Rock062_results'
}

def parse_filename_datetime(filename):
    """Extract datetime from filename like '2AFC_P_20251020_003915.csv'"""
    parts = os.path.basename(filename).split('_')
    if len(parts) >= 4:
        date_str = parts[2]  # '20251020'
        time_str = parts[3].split('.')[0]  # '003915'
        
        # Parse date and time
        year = int(date_str[:4])
        month = int(date_str[4:6])
        day = int(date_str[6:8])
        hour = int(time_str[:2])
        minute = int(time_str[2:4])
        second = int(time_str[4:6])
        
        return datetime(year, month, day, hour, minute, second)
    return None

def load_and_process_data(dataset_paths, num_recent=None):
    """Load all CSV files from specified dataset paths and process them"""
    all_data = {}
    
    for dataset_name, dataset_path in dataset_paths.items():
        if not os.path.exists(dataset_path):
            print(f"Warning: Dataset path '{dataset_path}' for '{dataset_name}' not found!")
            continue
            
        # Change to dataset directory
        original_dir = os.getcwd()
        os.chdir(dataset_path)
        
        try:
            csv_files = glob.glob("*.csv")
            csv_files = [f for f in csv_files if f.startswith('2AFC_P_')]
            
            # Sort by datetime and optionally limit to recent files
            file_info = []
            for file in csv_files:
                file_datetime = parse_filename_datetime(file)
                if file_datetime:
                    file_info.append((file, file_datetime))
            
            # Sort by datetime (newest first) and take recent ones if specified
            file_info.sort(key=lambda x: x[1], reverse=True)
            if num_recent and num_recent > 0:
                file_info = file_info[:num_recent]
            
            dataset_data = {}
            for file, file_datetime in file_info:
                try:
                    # Read CSV file
                    df = pd.read_csv(file)
                    
                    # Filter out rows where trial is NaN or empty (like the JND_px row)
                    df = df[df['trial'].notna()]
                    df = df[df['trial'] != '']
                    
                    # Convert trial to numeric and filter out non-numeric values
                    df['trial'] = pd.to_numeric(df['trial'], errors='coerce')
                    df = df[df['trial'].notna()]
                    
                    # Convert cmpPx to numeric
                    df['cmpPx'] = pd.to_numeric(df['cmpPx'], errors='coerce')
                    df = df[df['cmpPx'].notna()]
                    
                    if len(df) > 0:
                        dataset_data[file] = {
                            'data': df,
                            'datetime': file_datetime,
                            'filename': file,
                            'dataset': dataset_name
                        }
                except Exception as e:
                    print(f"Error processing {file} in {dataset_name}: {e}")
            
            if dataset_data:
                all_data[dataset_name] = dataset_data
                print(f"Loaded {len(dataset_data)} files from {dataset_name}")
            
        finally:
            os.chdir(original_dir)
    
    return all_data

def create_color_map(all_data):
    """Create color map from light blue (earliest) to dark red (latest) for each dataset"""
    colors = {}
    
    # Define distinct base colors for each dataset
    dataset_colors = {
        'bricks004': {'base': (0.2, 0.4, 0.8), 'name': 'Bricks004'},  # Blue
        'bricks101': {'base': (0.8, 0.2, 0.2), 'name': 'Bricks101'},  # Red
        'rock062': {'base': (0.2, 0.7, 0.2), 'name': 'Rock062'}       # Green
    }
    
    for dataset_name, dataset_data in all_data.items():
        if dataset_name not in dataset_colors:
            # Fallback color if dataset not in predefined colors
            dataset_colors[dataset_name] = {'base': (0.5, 0.5, 0.5), 'name': dataset_name}
        
        base_color = dataset_colors[dataset_name]['base']
        
        if len(dataset_data) == 1:
            # Single file, use base color
            filename = list(dataset_data.keys())[0]
            colors[filename] = base_color
        else:
            # Multiple files, create gradient
            datetimes = [info['datetime'] for info in dataset_data.values()]
            min_time = min(datetimes)
            max_time = max(datetimes)
            
            for filename, info in dataset_data.items():
                if max_time == min_time:
                    time_ratio = 0.5
                else:
                    time_ratio = (info['datetime'] - min_time).total_seconds() / (max_time - min_time).total_seconds()
                
                # Create gradient: darker (older) to lighter (newer)
                intensity = 0.4 + 0.6 * time_ratio  # Range from 0.4 to 1.0
                r, g, b = base_color
                colors[filename] = (r * intensity, g * intensity, b * intensity)
    
    return colors, dataset_colors

def plot_single_dataset(dataset_name, dataset_data, colors, dataset_colors, ax, global_xlim=None, global_ylim=None):
    """Plot a single dataset on the given axes"""
    if not dataset_data:
        ax.text(0.5, 0.5, f'No data for {dataset_name}', 
                transform=ax.transAxes, ha='center', va='center', fontsize=12)
        ax.set_title(f'{dataset_colors.get(dataset_name, {}).get("name", dataset_name)}', 
                     fontsize=14, fontweight='bold')
        return
    
    # Sort files by datetime for consistent plotting order
    sorted_files = sorted(dataset_data.keys(), key=lambda x: dataset_data[x]['datetime'])
    
    # Store data for average calculation
    all_trials = []
    all_cmpPx = []
    
    # Plot individual file lines
    for filename in sorted_files:
        info = dataset_data[filename]
        df = info['data']
        color = colors[filename]
        
        # Create label with datetime
        datetime_str = info['datetime'].strftime('%m/%d %H:%M')
        label = f"{datetime_str}"
        
        # Plot the line
        ax.plot(df['trial'], df['cmpPx'], 
                color=color, alpha=0.7, linewidth=1.5, 
                marker='o', markersize=3, label=label)
        
        # Store data for average
        all_trials.extend(df['trial'].tolist())
        all_cmpPx.extend(df['cmpPx'].tolist())
    
    # Calculate and plot average line
    if all_trials and all_cmpPx:
        # Create bins for trials to calculate average
        max_trial = max(all_trials)
        trial_bins = np.arange(1, max_trial + 1)
        avg_cmpPx = []
        
        for trial in trial_bins:
            # Find all cmpPx values for this trial number across all files
            trial_values = []
            for filename in dataset_data.keys():
                df = dataset_data[filename]['data']
                matching_rows = df[df['trial'] == trial]
                if not matching_rows.empty:
                    trial_values.extend(matching_rows['cmpPx'].tolist())
            
            if trial_values:
                avg_cmpPx.append(np.mean(trial_values))
            else:
                avg_cmpPx.append(np.nan)
        
        # Remove NaN values for plotting
        valid_indices = ~np.isnan(avg_cmpPx)
        valid_trials = trial_bins[valid_indices]
        valid_avg = np.array(avg_cmpPx)[valid_indices]
        
        if len(valid_trials) > 0:
            ax.plot(valid_trials, valid_avg, 
                    color='black', linewidth=3, 
                    label='Average', alpha=0.8, linestyle='--')
    
    # Customize the plot
    ax.set_xlabel('Trial Number', fontsize=10, fontweight='bold')
    ax.set_ylabel('Comparison Pixel Value (cmpPx)', fontsize=10, fontweight='bold')
    ax.set_title(f'{dataset_colors.get(dataset_name, {}).get("name", dataset_name)}', 
                 fontsize=12, fontweight='bold')
    
    # Add grid
    ax.grid(True, alpha=0.3, linestyle='-', linewidth=0.5)
    
    # Set axis limits - use global limits if provided, otherwise local limits with task max
    if global_xlim and global_ylim:
        ax.set_xlim(global_xlim)
        ax.set_ylim(global_ylim)
    elif all_trials and all_cmpPx:
        ax.set_xlim(0, max(all_trials) + 1)
        # Use task maximum (1024) for consistent scaling
        ax.set_ylim(min(all_cmpPx) - 10, 1024)
    
    # Add legend
    ax.legend(fontsize=8, title='Sessions', title_fontsize=9)
    
    # Add text box with summary statistics
    num_sessions = len(dataset_data)
    total_trials = sum(len(info['data']) for info in dataset_data.values())
    if dataset_data:
        date_range = f"{min(info['datetime'] for info in dataset_data.values()).strftime('%m/%d')} - {max(info['datetime'] for info in dataset_data.values()).strftime('%m/%d')}"
        summary_text = f'Sessions: {num_sessions}\nTrials: {total_trials}\nRange: {date_range}'
        ax.text(0.02, 0.98, summary_text, transform=ax.transAxes, 
                verticalalignment='top', bbox=dict(boxstyle='round', facecolor='wheat', alpha=0.8),
                fontsize=8)

def plot_data(datasets=None, num_recent=None):
    """Create the main plot(s)"""
    # Determine which datasets to load
    if datasets is None:
        # Default: load all datasets
        dataset_paths = DATASETS.copy()
    else:
        # Load only specified datasets
        dataset_paths = {name: path for name, path in DATASETS.items() if name in datasets}
    
    if not dataset_paths:
        print("No valid datasets specified!")
        return
    
    # Load data
    all_data = load_and_process_data(dataset_paths, num_recent)
    
    if not all_data:
        print("No valid CSV files found!")
        return
    
    # Create color map
    colors, dataset_colors = create_color_map(all_data)
    
    # Calculate global axis limits for consistent comparison
    global_xlim = None
    global_ylim = None
    
    if len(all_data) > 1:  # Only apply global limits when comparing multiple datasets
        all_trials_global = []
        all_cmpPx_global = []
        
        for dataset_name, dataset_data in all_data.items():
            for filename, info in dataset_data.items():
                df = info['data']
                all_trials_global.extend(df['trial'].tolist())
                all_cmpPx_global.extend(df['cmpPx'].tolist())
        
        if all_trials_global and all_cmpPx_global:
            global_xlim = (0, max(all_trials_global) + 1)
            # Set Y-axis from minimum data value to task maximum (1024)
            global_ylim = (min(all_cmpPx_global) - 10, 1024)
    
    # Determine plot layout
    num_datasets = len(all_data)
    
    if num_datasets == 1:
        # Single dataset - use full window
        fig, ax = plt.subplots(figsize=(12, 8))
        dataset_name = list(all_data.keys())[0]
        plot_single_dataset(dataset_name, all_data[dataset_name], colors, dataset_colors, ax, global_xlim, global_ylim)
    else:
        # Multiple datasets - side by side
        fig, axes = plt.subplots(1, num_datasets, figsize=(6 * num_datasets, 6))
        if num_datasets == 2:
            axes = [axes[0], axes[1]]  # Ensure axes is always a list
        elif num_datasets == 1:
            axes = [axes]
        
        for i, (dataset_name, dataset_data) in enumerate(all_data.items()):
            plot_single_dataset(dataset_name, dataset_data, colors, dataset_colors, axes[i], global_xlim, global_ylim)
    
    # Add overall title
    if num_recent:
        title = f'2AFC Experiment Results (Recent {num_recent} files per dataset)'
    else:
        title = '2AFC Experiment Results (All files)'
    
    # Add subtitle for consistent axis scaling
    if len(all_data) > 1:
        title += '\n(Y-axis scaled consistently: 0-1024 task range)'
    else:
        title += '\n(Y-axis scaled to task maximum: 1024)'
    
    fig.suptitle(title, fontsize=16, fontweight='bold')
    
    # Adjust layout
    plt.tight_layout()
    plt.subplots_adjust(top=0.88)  # Make room for suptitle with potential subtitle
    
    # Show the plot
    plt.show()
    
    # Print summary information including axis limits
    print("\n=== Data Summary ===")
    total_files = 0
    total_trials = 0
    
    if global_xlim and global_ylim:
        print(f"Global axis limits applied for comparison:")
        print(f"  X-axis (Trials): {global_xlim[0]:.0f} to {global_xlim[1]:.0f}")
        print(f"  Y-axis (cmpPx): {global_ylim[0]:.0f} to {global_ylim[1]:.0f} (task maximum)")
        print()
    else:
        print(f"Y-axis scaled to task maximum (1024) for consistency")
        print()
    
    for dataset_name, dataset_data in all_data.items():
        dataset_files = len(dataset_data)
        dataset_trials = sum(len(info['data']) for info in dataset_data.values())
        total_files += dataset_files
        total_trials += dataset_trials
        
        # Calculate dataset-specific ranges
        dataset_trials_list = []
        dataset_cmpPx_list = []
        for info in dataset_data.values():
            df = info['data']
            dataset_trials_list.extend(df['trial'].tolist())
            dataset_cmpPx_list.extend(df['cmpPx'].tolist())
        
        print(f"\n{dataset_colors.get(dataset_name, {}).get('name', dataset_name)}:")
        print(f"  Files processed: {dataset_files}")
        print(f"  Total trials: {dataset_trials}")
        
        if dataset_trials_list and dataset_cmpPx_list:
            print(f"  Trial range: {min(dataset_trials_list)} to {max(dataset_trials_list)}")
            print(f"  cmpPx range: {min(dataset_cmpPx_list):.1f} to {max(dataset_cmpPx_list):.1f}")
        
        if dataset_data:
            # Sort files by datetime for display
            sorted_files = sorted(dataset_data.items(), key=lambda x: x[1]['datetime'])
            for filename, info in sorted_files:
                df = info['data']
                print(f"  {filename}: {len(df)} trials, {info['datetime'].strftime('%Y-%m-%d %H:%M:%S')}")
    
    print(f"\nOverall Summary:")
    print(f"Total datasets: {len(all_data)}")
    print(f"Total files: {total_files}")
    print(f"Total trials: {total_trials}")

def main():
    """Main function with argument parsing"""
    parser = argparse.ArgumentParser(description='Plot 2AFC experiment results')
    parser.add_argument('--datasets', '-d', nargs='*', 
                       choices=['bricks004', 'bricks101', 'rock062'],
                       help='Datasets to plot (default: all)')
    parser.add_argument('--recent', '-r', type=int, 
                       help='Number of recent files to plot per dataset (default: all)')
    
    args = parser.parse_args()
    
    # Interactive mode if no arguments provided
    if len(os.sys.argv) == 1:
        print("2AFC Data Plotter")
        print("================")
        print("Available datasets:")
        for name, path in DATASETS.items():
            print(f"  {name}: {path}")
        
        print("\nSelect datasets to plot:")
        print("1. All datasets (default)")
        print("2. Bricks004 only")
        print("3. Bricks101 only") 
        print("4. Rock062 only")
        print("5. Custom selection")
        
        choice = input("\nEnter choice (1-5) or press Enter for default: ").strip()
        
        datasets = None
        if choice == '2':
            datasets = ['bricks004']
        elif choice == '3':
            datasets = ['bricks101']
        elif choice == '4':
            datasets = ['rock062']
        elif choice == '5':
            print("\nEnter dataset names separated by spaces:")
            print("Available: bricks004, bricks101, rock062")
            custom_input = input("Datasets: ").strip()
            if custom_input:
                datasets = custom_input.split()
        
        # Ask for number of recent files
        recent_input = input("\nNumber of recent files per dataset (press Enter for all): ").strip()
        num_recent = None
        if recent_input.isdigit():
            num_recent = int(recent_input)
        
        plot_data(datasets, num_recent)
    else:
        plot_data(args.datasets, args.recent)

if __name__ == "__main__":
    main()

import pandas as pd
import matplotlib.pyplot as plt
import numpy as np
import glob
import os
from datetime import datetime
import seaborn as sns
from scipy.optimize import curve_fit
from scipy.stats import norm
import warnings

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
                    
                    # Convert correct to boolean
                    df['correct'] = df['correct'].astype(bool)
                    
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

def logistic_function(x, a, b, c, d):
    """
    Logistic function for psychophysical curve fitting
    x: stimulus intensity (cmpPx)
    a: lower asymptote (minimum performance)
    b: steepness of the curve
    c: inflection point (threshold)
    d: upper asymptote (maximum performance)
    """
    return a + (d - a) / (1 + np.exp(-b * (x - c)))

def cumulative_normal(x, mu, sigma):
    """
    Cumulative normal distribution for psychophysical curve fitting
    x: stimulus intensity (cmpPx)
    mu: mean (threshold at 50%)
    sigma: standard deviation (relates to slope)
    """
    return norm.cdf(x, mu, sigma)

def fit_psychophysical_curve(cmpPx_values, correct_responses, method='logistic'):
    """
    Fit a psychophysical curve to the data
    Returns fitted parameters and R-squared
    """
    # Create bins for stimulus values
    unique_cmpPx = np.sort(np.unique(cmpPx_values))
    
    if len(unique_cmpPx) < 4:
        print(f"Warning: Only {len(unique_cmpPx)} unique stimulus values. Need at least 4 for reliable fitting.")
        return None, None, None, None, None, None, None
    
    # Calculate proportion correct for each stimulus level
    x_data = []
    y_data = []
    n_trials = []
    
    for cmpPx in unique_cmpPx:
        mask = cmpPx_values == cmpPx
        trials_at_level = np.sum(mask)
        if trials_at_level >= 2:  # Only include levels with at least 2 trials
            correct_at_level = np.sum(correct_responses[mask])
            prop_correct = correct_at_level / trials_at_level
            
            x_data.append(cmpPx)
            y_data.append(prop_correct)
            n_trials.append(trials_at_level)
    
    x_data = np.array(x_data)
    y_data = np.array(y_data)
    n_trials = np.array(n_trials)
    
    if len(x_data) < 4:
        print(f"Warning: Only {len(x_data)} stimulus levels with sufficient trials.")
        return None, None, None, None, None, None, None
    
    try:
        if method == 'logistic':
            # Improved initial parameter guesses
            x_range = np.max(x_data) - np.min(x_data)
            x_mid = np.min(x_data) + x_range * 0.5
            
            # Find approximate threshold from data
            # Look for the stimulus level where performance crosses 70%
            threshold_guess = x_mid
            for i, perf in enumerate(y_data):
                if perf >= 0.7:
                    threshold_guess = x_data[i]
                    break
            
            # Initial parameter guesses for logistic function
            # a=0.5 (chance level), b=slope, c=threshold, d=max performance
            max_perf = np.max(y_data)
            min_perf = np.min(y_data)
            slope_guess = 4.0 / x_range  # Reasonable slope
            
            p0 = [min_perf, slope_guess, threshold_guess, max_perf]
            
            # Set bounds
            bounds = ([0.4, 0.001, np.min(x_data), min_perf], 
                     [0.6, 100/x_range, np.max(x_data), 1.0])
            
            # Fit with weights based on number of trials and avoid extreme values
            weights = np.sqrt(n_trials) * (1 - np.abs(y_data - 0.5) * 0.5)  # Down-weight extreme values
            
            with warnings.catch_warnings():
                warnings.simplefilter("ignore")
                popt, pcov = curve_fit(logistic_function, x_data, y_data, p0=p0, bounds=bounds, 
                                     sigma=1/weights, absolute_sigma=False, maxfev=5000)
            
            # Calculate R-squared
            y_pred = logistic_function(x_data, *popt)
            ss_res = np.sum((y_data - y_pred) ** 2)
            ss_tot = np.sum((y_data - np.mean(y_data)) ** 2)
            r_squared = 1 - (ss_res / ss_tot) if ss_tot > 0 else 0
            
            # Calculate threshold at 70.7% correct (for 2-down-1-up staircase)
            target_performance = 0.707
            a, b, c, d = popt
            
            if a < target_performance < d and b > 0:  # Check if threshold is achievable
                threshold_707 = c - np.log((d - a)/(target_performance - a) - 1) / b
                # Check if threshold is within reasonable range
                if threshold_707 < np.min(x_data) or threshold_707 > np.max(x_data):
                    threshold_707 = np.nan
            else:
                threshold_707 = np.nan
            
            return popt, pcov, r_squared, threshold_707, x_data, y_data, n_trials
            
        elif method == 'cumulative_normal':
            # Initial parameter guesses for cumulative normal
            p0 = [np.median(x_data), np.std(x_data)]
            
            # Fit with weights based on number of trials
            popt, pcov = curve_fit(cumulative_normal, x_data, y_data, p0=p0, 
                                 sigma=1/np.sqrt(n_trials), absolute_sigma=False)
            
            # Calculate R-squared
            y_pred = cumulative_normal(x_data, *popt)
            ss_res = np.sum((y_data - y_pred) ** 2)
            ss_tot = np.sum((y_data - np.mean(y_data)) ** 2)
            r_squared = 1 - (ss_res / ss_tot) if ss_tot > 0 else 0
            
            # Calculate threshold at 70.7% correct
            threshold_707 = norm.ppf(0.707, popt[0], popt[1])
            
            return popt, pcov, r_squared, threshold_707, x_data, y_data, n_trials
            
    except Exception as e:
        print(f"Error fitting curve: {e}")
        return None, None, None, None, None, None, None

def simple_threshold_estimate(cmpPx_values, correct_responses, target_performance=0.707):
    """
    Simple threshold estimation using interpolation
    This is often more robust for staircase data than curve fitting
    """
    # Create bins for stimulus values
    unique_cmpPx = np.sort(np.unique(cmpPx_values))
    
    # Calculate proportion correct for each stimulus level
    x_data = []
    y_data = []
    n_trials = []
    
    for cmpPx in unique_cmpPx:
        mask = cmpPx_values == cmpPx
        trials_at_level = np.sum(mask)
        if trials_at_level >= 2:  # Only include levels with at least 2 trials
            correct_at_level = np.sum(correct_responses[mask])
            prop_correct = correct_at_level / trials_at_level
            
            x_data.append(cmpPx)
            y_data.append(prop_correct)
            n_trials.append(trials_at_level)
    
    x_data = np.array(x_data)
    y_data = np.array(y_data)
    n_trials = np.array(n_trials)
    
    if len(x_data) < 3:
        return np.nan, x_data, y_data, n_trials
    
    # Find points around the target performance
    below_target = y_data < target_performance
    above_target = y_data >= target_performance
    
    if not np.any(below_target) or not np.any(above_target):
        # All points are above or below target - use extrapolation
        if np.all(y_data >= target_performance):
            # All above target - threshold is below minimum stimulus
            threshold = np.min(x_data) - (np.min(x_data) * 0.1)
        else:
            # All below target - threshold is above maximum stimulus
            threshold = np.max(x_data) + (np.max(x_data) * 0.1)
    else:
        # Interpolate between points
        # Find the transition point
        transition_idx = np.where(above_target)[0][0]
        
        if transition_idx == 0:
            # First point is already above target
            threshold = x_data[0]
        else:
            # Interpolate between points around the target
            x1, y1 = x_data[transition_idx - 1], y_data[transition_idx - 1]
            x2, y2 = x_data[transition_idx], y_data[transition_idx]
            
            # Linear interpolation
            if y2 != y1:
                threshold = x1 + (target_performance - y1) * (x2 - x1) / (y2 - y1)
            else:
                threshold = (x1 + x2) / 2
    
    return threshold, x_data, y_data, n_trials

def analyze_dataset(dataset_name, dataset_data, method='logistic'):
    """Analyze a single dataset and fit psychophysical curves"""
    print(f"\n=== Analyzing {dataset_name.upper()} ===")
    
    # Combine all data from all sessions in this dataset
    all_cmpPx = []
    all_correct = []
    session_info = []
    
    for filename, info in dataset_data.items():
        df = info['data']
        all_cmpPx.extend(df['cmpPx'].values)
        all_correct.extend(df['correct'].values)
        session_info.append({
            'filename': filename,
            'datetime': info['datetime'],
            'trials': len(df),
            'accuracy': np.mean(df['correct'])
        })
    
    all_cmpPx = np.array(all_cmpPx)
    all_correct = np.array(all_correct)
    
    print(f"Total trials: {len(all_cmpPx)}")
    print(f"Overall accuracy: {np.mean(all_correct):.3f}")
    print(f"Stimulus range: {np.min(all_cmpPx):.1f} to {np.max(all_cmpPx):.1f}")
    
    # Simple threshold estimation (often more robust for staircase data)
    simple_threshold, x_simple, y_simple, n_simple = simple_threshold_estimate(all_cmpPx, all_correct)
    print(f"\nSimple threshold estimate (70.7%): {simple_threshold:.1f} pixels")
    
    # Fit psychophysical curve
    fit_result = fit_psychophysical_curve(all_cmpPx, all_correct, method)
    
    if fit_result[0] is not None:
        popt, pcov, r_squared, threshold_707, x_data, y_data, n_trials = fit_result
        
        print(f"\nPsychophysical curve fitting ({method}):")
        print(f"R-squared: {r_squared:.3f}")
        
        if method == 'logistic':
            print(f"Parameters: a={popt[0]:.3f}, b={popt[1]:.4f}, c={popt[2]:.1f}, d={popt[3]:.3f}")
            if not np.isnan(threshold_707):
                print(f"Curve-fitted threshold (70.7%): {threshold_707:.1f} pixels")
            else:
                print("Curve-fitted threshold: Could not determine (curve may not reach 70.7%)")
        elif method == 'cumulative_normal':
            print(f"Parameters: mu={popt[0]:.1f}, sigma={popt[1]:.1f}")
            print(f"Threshold (70.7%): {threshold_707:.1f} pixels")
        
        return {
            'dataset': dataset_name,
            'n_trials': len(all_cmpPx),
            'overall_accuracy': np.mean(all_correct),
            'stimulus_range': (np.min(all_cmpPx), np.max(all_cmpPx)),
            'simple_threshold': simple_threshold,
            'fit_params': popt,
            'r_squared': r_squared,
            'threshold_707': threshold_707,
            'x_data': x_data,
            'y_data': y_data,
            'n_trials_per_level': n_trials,
            'session_info': session_info,
            'method': method
        }
    else:
        print("Failed to fit psychophysical curve")
        return {
            'dataset': dataset_name,
            'n_trials': len(all_cmpPx),
            'overall_accuracy': np.mean(all_correct),
            'stimulus_range': (np.min(all_cmpPx), np.max(all_cmpPx)),
            'simple_threshold': simple_threshold,
            'fit_params': None,
            'r_squared': None,
            'threshold_707': np.nan,
            'x_data': x_simple,
            'y_data': y_simple,
            'n_trials_per_level': n_simple,
            'session_info': session_info,
            'method': method
        }

def plot_psychophysical_curves(results, save_plot=True):
    """Plot psychophysical curves for all datasets"""
    
    # Filter out None results
    valid_results = [r for r in results if r is not None]
    
    if not valid_results:
        print("No valid results to plot")
        return
    
    n_datasets = len(valid_results)
    
    # Create subplots
    fig, axes = plt.subplots(1, n_datasets, figsize=(6 * n_datasets, 6))
    
    if n_datasets == 1:
        axes = [axes]
    
    # Define colors for each dataset
    colors = {
        'bricks004': '#1f77b4',  # Blue
        'bricks101': '#d62728',  # Red
        'rock062': '#2ca02c'     # Green
    }
    
    for i, result in enumerate(valid_results):
        ax = axes[i]
        dataset_name = result['dataset']
        color = colors.get(dataset_name, '#7f7f7f')
        
        # Plot data points
        x_data = result['x_data']
        y_data = result['y_data']
        n_trials = result['n_trials_per_level']
        
        # Size points by number of trials
        sizes = 50 + (n_trials - np.min(n_trials)) / (np.max(n_trials) - np.min(n_trials) + 1) * 150
        
        ax.scatter(x_data, y_data, s=sizes, alpha=0.7, color=color, 
                  edgecolors='black', linewidth=1, label='Data')
        
        # Plot fitted curve
        if result['fit_params'] is not None:
            x_smooth = np.linspace(np.min(x_data), np.max(x_data), 100)
            
            if result['method'] == 'logistic':
                y_smooth = logistic_function(x_smooth, *result['fit_params'])
            elif result['method'] == 'cumulative_normal':
                y_smooth = cumulative_normal(x_smooth, *result['fit_params'])
            
            ax.plot(x_smooth, y_smooth, color=color, linewidth=2, label='Fitted curve')
            
            # Mark threshold from curve fitting
            fitted_threshold = result['threshold_707']
            if not np.isnan(fitted_threshold):
                ax.axvline(fitted_threshold, color='red', linestyle='--', alpha=0.7, 
                          label=f'Fitted 70.7%: {fitted_threshold:.1f}')
        
        # Mark simple threshold estimate
        simple_threshold = result['simple_threshold']
        if not np.isnan(simple_threshold):
            ax.axvline(simple_threshold, color='orange', linestyle='-', alpha=0.7, 
                      label=f'Simple 70.7%: {simple_threshold:.1f}')
            ax.axhline(0.707, color='red', linestyle=':', alpha=0.5)
        
        # Mark chance level
        ax.axhline(0.5, color='gray', linestyle=':', alpha=0.5, label='Chance level')
        
        # Formatting
        ax.set_xlabel('Stimulus Intensity (cmpPx)', fontsize=11)
        ax.set_ylabel('Proportion Correct', fontsize=11)
        
        r_squared_str = f"R² = {result['r_squared']:.3f}" if result['r_squared'] is not None else "Fit failed"
        ax.set_title(f'{dataset_name.upper()}\n({r_squared_str})', fontsize=12, fontweight='bold')
        
        ax.grid(True, alpha=0.3)
        ax.set_ylim(0.4, 1.05)
        ax.legend(fontsize=8)
        
        # Add summary text
        simple_thresh = result['simple_threshold']
        simple_str = f"{simple_thresh:.1f}" if not np.isnan(simple_thresh) else "N/A"
        summary_text = f'Trials: {result["n_trials"]}\nAccuracy: {result["overall_accuracy"]:.3f}\nSimple thresh: {simple_str}'
        ax.text(0.02, 0.98, summary_text, transform=ax.transAxes, 
               verticalalignment='top', bbox=dict(boxstyle='round', facecolor='wheat', alpha=0.8),
               fontsize=9)
    
    # Overall title
    fig.suptitle('Psychophysical Curves for 2AFC Task\n(70.7% threshold for 2-down-1-up staircase)', 
                 fontsize=14, fontweight='bold')
    
    plt.tight_layout()
    plt.subplots_adjust(top=0.85)
    
    if save_plot:
        plt.savefig('psychophysical_curves.png', dpi=300, bbox_inches='tight')
        print(f"\nPlot saved as 'psychophysical_curves.png'")
    
    plt.show()

def main():
    """Main analysis function"""
    print("2AFC Psychophysical Curve Analysis")
    print("==================================")
    
    # Load all datasets
    all_data = load_and_process_data(DATASETS)
    
    if not all_data:
        print("No data loaded!")
        return
    
    # Analyze each dataset
    results = []
    threshold_summary = []
    
    for dataset_name, dataset_data in all_data.items():
        result = analyze_dataset(dataset_name, dataset_data, method='logistic')
        results.append(result)
        
        if result:
            threshold_summary.append({
                'dataset': dataset_name,
                'simple_threshold': result['simple_threshold'],
                'fitted_threshold': result['threshold_707'] if not np.isnan(result['threshold_707']) else None,
                'r_squared': result['r_squared'],
                'n_trials': result['n_trials']
            })
    
    # Plot results
    plot_psychophysical_curves(results)
    
    # Summary table
    print("\n" + "="*80)
    print("THRESHOLD SUMMARY (70.7% correct level)")
    print("="*80)
    
    if threshold_summary:
        print(f"{'Dataset':<12} {'Simple Est.':<12} {'Fitted Est.':<12} {'R²':<8} {'N Trials':<10}")
        print("-" * 70)
        
        simple_thresholds = []
        fitted_thresholds = []
        
        for item in threshold_summary:
            simple_thresh = item['simple_threshold']
            fitted_thresh = item['fitted_threshold']
            r_squared = item['r_squared'] if item['r_squared'] is not None else 0
            
            simple_str = f"{simple_thresh:.1f}" if not np.isnan(simple_thresh) else "N/A"
            fitted_str = f"{fitted_thresh:.1f}" if fitted_thresh is not None else "N/A"
            r_squared_str = f"{r_squared:.3f}" if item['r_squared'] is not None else "N/A"
            
            print(f"{item['dataset']:<12} {simple_str:<12} {fitted_str:<12} {r_squared_str:<8} {item['n_trials']:<10}")
            
            if not np.isnan(simple_thresh):
                simple_thresholds.append(simple_thresh)
            if fitted_thresh is not None:
                fitted_thresholds.append(fitted_thresh)
        
        # Compare thresholds
        print(f"\nThreshold comparison:")
        if len(simple_thresholds) > 1:
            print(f"Simple estimates - Range: {np.min(simple_thresholds):.1f} - {np.max(simple_thresholds):.1f} pixels")
            print(f"                  Mean: {np.mean(simple_thresholds):.1f} ± {np.std(simple_thresholds):.1f} pixels")
        
        if len(fitted_thresholds) > 1:
            print(f"Fitted estimates - Range: {np.min(fitted_thresholds):.1f} - {np.max(fitted_thresholds):.1f} pixels")
            print(f"                  Mean: {np.mean(fitted_thresholds):.1f} ± {np.std(fitted_thresholds):.1f} pixels")
        
        if len(simple_thresholds) > 1:
            # Find relative differences using simple estimates
            baseline = simple_thresholds[0]
            dataset_names = [item['dataset'] for item in threshold_summary if not np.isnan(item['simple_threshold'])]
            print(f"\nRelative threshold ratios (using simple estimates):")
            for i, thresh in enumerate(simple_thresholds):
                if i > 0:
                    ratio = thresh / baseline
                    print(f"{dataset_names[i]} vs {dataset_names[0]}: {ratio:.2f}x")
    else:
        print("No valid thresholds found!")

if __name__ == "__main__":
    main()
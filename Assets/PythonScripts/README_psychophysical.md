# 2AFC Psychophysical Curve Analysis

This script analyzes 2AFC (Two Alternative Forced Choice) experiment data to estimate perceptual thresholds using psychophysical curve fitting. It's specifically designed for 2-down-1-up staircase procedures that converge to 70.7% correct performance.

## Features

- **Dual Threshold Estimation**: 
  - Simple interpolation method (robust for staircase data)
  - Logistic curve fitting (when sufficient data points available)
- **Multi-Dataset Analysis**: Analyzes Bricks004, Bricks101, and Rock062 datasets simultaneously
- **Comprehensive Visualization**: Side-by-side psychophysical curves with threshold markers
- **Statistical Summary**: R-squared values, parameter estimates, and threshold comparisons

## Key Concepts

### 2-Down-1-Up Staircase
- Increases difficulty after 2 consecutive correct responses
- Decreases difficulty after 1 incorrect response  
- Converges to **70.7% correct performance** (not 75%)
- This is the target threshold for analysis

### Threshold Estimation Methods

1. **Simple Interpolation** (Orange line)
   - Finds where performance crosses 70.7% by interpolating between data points
   - More robust for staircase data with limited sampling
   - Less sensitive to outliers

2. **Logistic Curve Fitting** (Red dashed line)  
   - Fits full psychophysical curve: `a + (d-a) / (1 + exp(-b*(x-c)))`
   - Provides smooth threshold estimate when fit is good (high R²)
   - May fail with sparse or noisy data

## Usage

```bash
# Run analysis on all datasets
python psychophysical_analysis.py
```

The script automatically:
1. Loads all CSV files from the three dataset folders
2. Combines data across sessions within each dataset
3. Estimates thresholds using both methods
4. Plots psychophysical curves
5. Provides comparative summary

## Output

### Console Output
```
=== Analyzing BRICKS004 ===
Total trials: 422
Overall accuracy: 0.917
Stimulus range: 4.0 to 1020.0

Simple threshold estimate (70.7%): 4.3 pixels

Psychophysical curve fitting (logistic):
R-squared: -0.070
Parameters: a=0.600, b=0.0168, c=6.7, d=0.928
Curve-fitted threshold: Could not determine
```

### Visual Output
- **psychophysical_curves.png**: Side-by-side plots showing:
  - Data points (size = number of trials at that level)
  - Fitted psychophysical curves (when successful)
  - Threshold markers (orange = simple, red = fitted)
  - 70.7% performance line
  - Chance level (50%) reference

### Summary Table
```
Dataset      Simple Est.  Fitted Est.  R²       N Trials  
bricks004    4.3          N/A          N/A      422       
bricks101    5.1          5.7          0.030    1017      
rock062      7.8          N/A          N/A      703       
```

## Interpretation Guidelines

### Threshold Reliability
- **Simple estimates**: Generally more reliable for staircase data
- **Fitted estimates**: Only trust when R² > 0.5
- **Low R² values**: Common with staircase data due to adaptive sampling

### Comparing Conditions
- Lower threshold = easier to detect difference
- Threshold ratios show relative difficulty
- Example: Rock062 vs Bricks004 = 1.8x harder

### Expected Results
For 2AFC texture discrimination:
- Thresholds typically 5-50 pixels depending on:
  - Texture complexity
  - Viewing conditions  
  - Individual differences
  - Task difficulty

## Technical Details

### Data Requirements
- CSV files with columns: `trial`, `cmpPx`, `correct`
- Minimum 20-30 trials per dataset for reliable estimation
- At least 4 different stimulus levels

### Logistic Function Parameters
- `a`: Lower asymptote (minimum performance ~50%)
- `b`: Slope steepness (larger = steeper curve) 
- `c`: Inflection point (50% threshold)
- `d`: Upper asymptote (maximum performance)

### Troubleshooting
- **"Fit failed"**: Use simple estimate instead
- **Very low R²**: Data may be too noisy or sparse
- **Threshold N/A**: Performance never reached 70.7%

## Dependencies

```bash
pip install pandas matplotlib numpy scipy seaborn
```

## Files Generated
- `psychophysical_curves.png`: Publication-ready figure
- Console output with detailed analysis results

This analysis helps quantify perceptual sensitivity differences across experimental conditions and provides statistical validation of threshold estimates.
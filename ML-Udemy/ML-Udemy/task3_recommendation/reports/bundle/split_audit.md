# Data Audit: Bundle Splitting

**Original Positive Interactions**: 222330
**Filtered Interactions (Users >= 5 positives)**: 222330
**Unique Users Retained**: 25000
**Unique Items in Train**: 106
**Unique Items in Val**: 102
**Unique Items in Test**: 104

## Splits
- **Train Size**: 172330
- **Validation Size**: 25000
- **Test Size**: 25000

**Leakage Check**: Are there any user-item pairs present in multiple splits?
- Train & Val intersection: 0
- Train & Test intersection: 0
- Val & Test intersection: 0
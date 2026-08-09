# Data Audit: Bundle Dataset

**File**: rating_df.csv
**Total Rows (interactions)**: 233306
**Columns**: 3

## Schema
| Column | Type | Non-Null Count | Null % |
|---|---|---|---|
| `user` | int64 | 233306 | 0.00% |
| `item` | str | 233306 | 0.00% |
| `rating` | int64 | 233306 | 0.00% |

## Statistics
- **Unique Users**: 33901
- **Unique Items**: 126
- **Rating == 1 (Positives)**: 222330
- **Rating == 0 (Explicit Zeros)**: 10976
- **Duplicate User-Item pairs**: 0
- **Interactions per User (avg)**: 6.88
- **Interactions per Item (avg)**: 1851.63
- **Matrix Sparsity**: 94.538111%
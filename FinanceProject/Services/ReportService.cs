using Microsoft.EntityFrameworkCore;
using FinanceManager.Data;
using FinanceManager.Models;
using FinanceManager.Models.ViewModels;
using DinkToPdf;
using DinkToPdf.Contracts;
using OfficeOpenXml;
using System.Text;

namespace FinanceManager.Services
{
    public interface IReportService
    {
        Task<FinancialSummaryViewModel> GetFinancialSummaryAsync(int userId, DateTime? startDate, DateTime? endDate);
        Task<CashFlowReportViewModel> GetCashFlowReportAsync(int userId, DateTime? startDate, DateTime? endDate);
        Task<BudgetReportViewModel> GetBudgetReportAsync(int userId, DateTime? startDate, DateTime? endDate);
        Task<byte[]> GenerateReportPdfAsync(ReportType reportType, int userId, DateTime? startDate, DateTime? endDate);
        Task<byte[]> GenerateReportExcelAsync(ReportType reportType, int userId, DateTime? startDate, DateTime? endDate);

    }

    public class ReportService : IReportService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConverter _pdfConverter;
        private const string CURRENCY_FORMAT = "#,##0.00";
        private const string PERCENTAGE_FORMAT = "0.0%";

        public ReportService(ApplicationDbContext context, IConverter pdfConverter)
        {
            _context = context;
            _pdfConverter = pdfConverter;
        }

        public async Task<FinancialSummaryViewModel> GetFinancialSummaryAsync(int userId, DateTime? startDate, DateTime? endDate)
        {
            var query = _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId);

            if (startDate.HasValue)
                query = query.Where(t => t.Date >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(t => t.Date <= endDate.Value);

            var transactions = await query.ToListAsync();

            var totalIncome = transactions.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);
            var totalExpenses = transactions.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount);

            var categoryTotals = transactions
                .GroupBy(t => t.Category.Name)
                .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));

            var monthlyTotals = transactions
                .GroupBy(t => new { t.Date.Year, t.Date.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .ToDictionary(
                    g => $"{g.Key.Year}-{g.Key.Month:D2}",
                    g => g.Sum(t => t.Type == TransactionType.Income ? t.Amount : -t.Amount)
                );

            var categoryPercentages = categoryTotals
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => totalExpenses > 0 ? (kvp.Value / totalExpenses) * 100 : 0
                );

            // Get budget progress
            var budgets = await _context.Budgets
                .Include(b => b.Category)
                .Where(b => b.UserId == userId)
                .ToListAsync();

            var budgetProgress = new Dictionary<string, decimal>();
            foreach (var budget in budgets)
            {
                var spent = transactions
                    .Where(t => t.CategoryId == budget.CategoryId)
                    .Sum(t => t.Amount);
                budgetProgress[budget.Category.Name] = budget.Amount > 0 ? (spent / budget.Amount) * 100 : 0;
            }

            // Get goal progress
            var goals = await _context.Goals
                .Where(g => g.UserId == userId && g.Status == GoalStatus.Active)
                .ToListAsync();

            var goalProgress = goals.ToDictionary(
                g => g.Name,
                g => g.TargetAmount > 0 ? (g.CurrentAmount / g.TargetAmount) * 100 : 0
            );

            return new FinancialSummaryViewModel
            {
                TotalIncome = totalIncome,
                TotalExpenses = totalExpenses,
                NetIncome = totalIncome - totalExpenses,
                CategoryTotals = categoryTotals,
                MonthlyTotals = monthlyTotals,
                CategoryPercentages = categoryPercentages,
                RecentTransactions = transactions.OrderByDescending(t => t.Date).Take(5),
                BudgetProgress = budgetProgress,
                GoalProgress = goalProgress
            };
        }

        public async Task<CashFlowReportViewModel> GetCashFlowReportAsync(int userId, DateTime? startDate, DateTime? endDate)
        {
            var query = _context.Transactions
                .Where(t => t.UserId == userId);

            if (startDate.HasValue)
                query = query.Where(t => t.Date >= startDate.Value);
            if (endDate.HasValue)
                query = query.Where(t => t.Date <= endDate.Value);

            var transactions = await query.ToListAsync();

            var monthlyData = transactions
                .GroupBy(t => new { t.Date.Year, t.Date.Month })
                .OrderBy(g => g.Key.Year)
                .ThenBy(g => g.Key.Month)
                .Select(g => new MonthlyTrend
                {
                    Month = $"{g.Key.Year}-{g.Key.Month:D2}",
                    Income = g.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount),
                    Expenses = g.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount),
                    Savings = g.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount) -
                             g.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount)
                })
                .ToList();

            foreach (var month in monthlyData)
            {
                month.SavingsRate = month.Income > 0 ? (month.Savings / month.Income) * 100 : 0;
            }

            return new CashFlowReportViewModel
            {
                MonthlyIncome = monthlyData.ToDictionary(m => m.Month, m => m.Income),
                MonthlyExpenses = monthlyData.ToDictionary(m => m.Month, m => m.Expenses),
                MonthlySavings = monthlyData.ToDictionary(m => m.Month, m => m.Savings),
                Trends = monthlyData
            };
        }

        public async Task<BudgetReportViewModel> GetBudgetReportAsync(int userId, DateTime? startDate, DateTime? endDate)
        {
            var budgets = await _context.Budgets
                .Include(b => b.Category)
                .Where(b => b.UserId == userId)
                .ToListAsync();

            var transactions = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId)
                .ToListAsync();

            if (startDate.HasValue)
                transactions = transactions.Where(t => t.Date >= startDate.Value).ToList();
            if (endDate.HasValue)
                transactions = transactions.Where(t => t.Date <= endDate.Value).ToList();

            var budgetSummaries = new List<BudgetSummary>();
            var monthlySpending = new Dictionary<string, List<decimal>>();

            foreach (var budget in budgets)
            {
                var spent = transactions
                    .Where(t => t.CategoryId == budget.CategoryId)
                    .Sum(t => t.Amount);

                budgetSummaries.Add(new BudgetSummary
                {
                    Category = budget.Category.Name,
                    Budgeted = budget.Amount,
                    Spent = spent,
                    Remaining = budget.Amount - spent,
                    UsagePercentage = budget.Amount > 0 ? (spent / budget.Amount) * 100 : 0
                });

                var monthlyAmounts = transactions
                    .Where(t => t.CategoryId == budget.CategoryId)
                    .GroupBy(t => new { t.Date.Year, t.Date.Month })
                    .OrderBy(g => g.Key.Year)
                    .ThenBy(g => g.Key.Month)
                    .Select(g => g.Sum(t => t.Amount))
                    .ToList();

                monthlySpending[budget.Category.Name] = monthlyAmounts;
            }

            return new BudgetReportViewModel
            {
                BudgetSummaries = budgetSummaries,
                TotalBudgeted = budgetSummaries.Sum(b => b.Budgeted),
                TotalSpent = budgetSummaries.Sum(b => b.Spent),
                TotalRemaining = budgetSummaries.Sum(b => b.Remaining),
                MonthlySpending = monthlySpending
            };

        }
        public async Task<byte[]> GenerateReportPdfAsync(ReportType reportType, int userId, DateTime? startDate, DateTime? endDate)
        {
            try
            {
                var htmlContent = new StringBuilder();
                htmlContent.AppendLine(@"
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset='UTF-8'>
                    <style>
                        body { font-family: Arial, sans-serif; margin: 20px; }
                        .header { text-align: center; margin-bottom: 20px; }
                        .table { width: 100%; border-collapse: collapse; margin: 15px 0; }
                        .table th, .table td { border: 1px solid #ddd; padding: 8px; text-align: left; }
                        .table th { background-color: #f5f5f5; }
                        .summary { margin: 20px 0; padding: 15px; background-color: #f9f9f9; }
                        .over-budget { background-color: #ffe6e6; }
                        .near-limit { background-color: #fff3e6; }
                        .text-right { text-align: right; }
                        .text-center { text-align: center; }
                    </style>
                </head>
                <body>");



                string content = string.Empty;
                switch (reportType)
                {
                    case ReportType.FinancialSummary:
                        var summary = await GetFinancialSummaryAsync(userId, startDate, endDate);
                        content = GenerateFinancialSummaryHtml(summary);
                        break;

                    case ReportType.CashFlow:
                        var cashFlow = await GetCashFlowReportAsync(userId, startDate, endDate);
                        content = GenerateCashFlowHtml(cashFlow);
                        break;

                    case ReportType.BudgetAnalysis:
                        var budget = await GetBudgetReportAsync(userId, startDate, endDate);
                        content = GenerateBudgetAnalysisHtml(budget);
                        break;

                    default:
                        throw new ArgumentException("Unsupported report type");
                }

                htmlContent.AppendLine(content);
                htmlContent.AppendLine("</body></html>");

                var doc = new HtmlToPdfDocument()
                {
                    GlobalSettings = {
                        ColorMode = ColorMode.Color,
                        Orientation = Orientation.Portrait,
                        PaperSize = PaperKind.A4,
                        Margins = new MarginSettings { Top = 10, Bottom = 10, Left = 10, Right = 10 }
                    },
                    Objects = {
                        new ObjectSettings {
                            PagesCount = true,
                            HtmlContent = htmlContent.ToString(),
                            WebSettings = { DefaultEncoding = "utf-8" }
                        }
                    }
                };

                return _pdfConverter.Convert(doc);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to generate PDF report: {ex.Message}", ex);
            }
        }

        public async Task<byte[]> GenerateReportExcelAsync(ReportType reportType, int userId, DateTime? startDate, DateTime? endDate)
        {
            try
            {
                using var package = new ExcelPackage();
                ExcelWorksheet worksheet;

                switch (reportType)
                {
                    case ReportType.FinancialSummary:
                        var summary = await GetFinancialSummaryAsync(userId, startDate, endDate);
                        worksheet = package.Workbook.Worksheets.Add("Financial Summary");
                        GenerateFinancialSummaryExcel(worksheet, summary);
                        break;

                    case ReportType.CashFlow:
                        var cashFlow = await GetCashFlowReportAsync(userId, startDate, endDate);
                        worksheet = package.Workbook.Worksheets.Add("Cash Flow");
                        GenerateCashFlowExcel(worksheet, cashFlow);
                        break;

                    case ReportType.BudgetAnalysis:
                        var budget = await GetBudgetReportAsync(userId, startDate, endDate);
                        worksheet = package.Workbook.Worksheets.Add("Budget Analysis");
                        GenerateBudgetAnalysisExcel(worksheet, budget);
                        break;

                    default:
                        throw new ArgumentException("Unsupported report type");
                }

                return package.GetAsByteArray();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to generate Excel report: {ex.Message}", ex);
            }
        }

        private void GenerateFinancialSummaryExcel(ExcelWorksheet worksheet, FinancialSummaryViewModel model)
        {
            try
            {
                // Set headers
                worksheet.Cells["A1"].Value = "Financial Summary Report";
                worksheet.Cells["A1:D1"].Merge = true;
                worksheet.Cells["A1:D1"].Style.Font.Bold = true;
                worksheet.Cells["A1:D1"].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

                // Overview section
                worksheet.Cells["A3"].Value = "Overview";
                worksheet.Cells["A3"].Style.Font.Bold = true;

                worksheet.Cells["A4"].Value = "Total Income:";
                worksheet.Cells["B4"].Value = model.TotalIncome;
                worksheet.Cells["A5"].Value = "Total Expenses:";
                worksheet.Cells["B5"].Value = model.TotalExpenses;
                worksheet.Cells["A6"].Value = "Net Income:";
                worksheet.Cells["B6"].Value = model.NetIncome;

                // Format overview section
                worksheet.Cells["B4:B6"].Style.Numberformat.Format = CURRENCY_FORMAT;

                // Only add category breakdown if there are categories
                if (model.CategoryTotals.Any())
                {
                    worksheet.Cells["A8"].Value = "Category Breakdown";
                    worksheet.Cells["A8"].Style.Font.Bold = true;

                    worksheet.Cells["A9"].Value = "Category";
                    worksheet.Cells["B9"].Value = "Amount";
                    worksheet.Cells["C9"].Value = "Percentage";
                    worksheet.Cells["A9:C9"].Style.Font.Bold = true;

                    var row = 10;
                    foreach (var category in model.CategoryTotals)
                    {
                        worksheet.Cells[$"A{row}"].Value = category.Key;
                        worksheet.Cells[$"B{row}"].Value = category.Value;
                        worksheet.Cells[$"C{row}"].Value = model.CategoryPercentages[category.Key] / 100;
                        row++;
                    }

                    // Format category section if there is data
                    if (row > 10)
                    {
                        var lastRow = row - 1;
                        worksheet.Cells[$"B10:B{lastRow}"].Style.Numberformat.Format = CURRENCY_FORMAT;
                        worksheet.Cells[$"C10:C{lastRow}"].Style.Numberformat.Format = PERCENTAGE_FORMAT;
                    }
                }

                // Auto-fit columns if we have data
                if (worksheet.Dimension != null)
                {
                    worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
                } 
            } 
            catch (Exception ex)
            {
                throw new Exception($"Failed to generate financial summary Excel: {ex.Message}", ex);
            }
        }
        private string GenerateFinancialSummaryHtml(FinancialSummaryViewModel model)
        {
            var html = new StringBuilder();

            html.AppendLine("<div class='header'><h1>Financial Summary Report</h1></div>");

            // Summary section
            html.AppendLine("<div class='summary'>");
            html.AppendLine("<h2>Overview</h2>");
            html.AppendLine($"<p>Total Income: <span class='text-right'>${model.TotalIncome:N2}</span></p>");
            html.AppendLine($"<p>Total Expenses: <span class='text-right'>${model.TotalExpenses:N2}</span></p>");
            html.AppendLine($"<p>Net Income: <span class='text-right'>${model.NetIncome:N2}</span></p>");
            html.AppendLine("</div>");

            if (model.CategoryTotals.Any())
            {
                // Category breakdown
                html.AppendLine("<div class='table-section'>");
                html.AppendLine("<h2>Category Breakdown</h2>");
                html.AppendLine("<table class='table'>");
                html.AppendLine("<tr><th>Category</th><th class='text-right'>Amount</th><th class='text-right'>Percentage</th></tr>");

                foreach (var category in model.CategoryTotals)
                {
                    html.AppendLine("<tr>");
                    html.AppendLine($"<td>{category.Key}</td>");
                    html.AppendLine($"<td class='text-right'>${category.Value:N2}</td>");
                    html.AppendLine($"<td class='text-right'>{model.CategoryPercentages[category.Key]:N1}%</td>");
                    html.AppendLine("</tr>");
                }

                html.AppendLine("</table>");
                html.AppendLine("</div>");
            }
            else
            {
                html.AppendLine("<p class='text-center'>No category data available for the selected period.</p>");
            }

            return html.ToString();
        }

        private string GenerateCashFlowHtml(CashFlowReportViewModel model)
        {
            var html = new StringBuilder();

            html.AppendLine("<div class='header'><h1>Cash Flow Report</h1></div>");

            if (model.Trends.Any())
            {
                // Monthly Trends Table
                html.AppendLine("<div class='table-section'>");
                html.AppendLine("<h2>Monthly Cash Flow</h2>");
                html.AppendLine("<table class='table'>");
                html.AppendLine("<tr>");
                html.AppendLine("<th>Month</th>");
                html.AppendLine("<th class='text-right'>Income</th>");
                html.AppendLine("<th class='text-right'>Expenses</th>");
                html.AppendLine("<th class='text-right'>Savings</th>");
                html.AppendLine("<th class='text-right'>Savings Rate</th>");
                html.AppendLine("</tr>");

                foreach (var trend in model.Trends)
                {
                    html.AppendLine("<tr>");
                    html.AppendLine($"<td>{trend.Month}</td>");
                    html.AppendLine($"<td class='text-right'>${trend.Income:N2}</td>");
                    html.AppendLine($"<td class='text-right'>${trend.Expenses:N2}</td>");
                    html.AppendLine($"<td class='text-right'>${trend.Savings:N2}</td>");
                    html.AppendLine($"<td class='text-right'>{trend.SavingsRate:N1}%</td>");
                    html.AppendLine("</tr>");
                }

                html.AppendLine("</table>");
                html.AppendLine("</div>");

                // Summary Statistics
                html.AppendLine("<div class='summary'>");
                html.AppendLine("<h2>Summary Statistics</h2>");

                var averageIncome = model.Trends.Average(t => t.Income);
                var averageExpenses = model.Trends.Average(t => t.Expenses);
                var averageSavings = model.Trends.Average(t => t.Savings);
                var averageSavingsRate = model.Trends.Average(t => t.SavingsRate);

                html.AppendLine($"<p>Average Monthly Income: <span class='text-right'>${averageIncome:N2}</span></p>");
                html.AppendLine($"<p>Average Monthly Expenses: <span class='text-right'>${averageExpenses:N2}</span></p>");
                html.AppendLine($"<p>Average Monthly Savings: <span class='text-right'>${averageSavings:N2}</span></p>");
                html.AppendLine($"<p>Average Savings Rate: <span class='text-right'>{averageSavingsRate:N1}%</span></p>");
                html.AppendLine("</div>");
            }
            else
            {
                html.AppendLine("<p class='text-center'>No cash flow data available for the selected period.</p>");
            }

            return html.ToString();
        }

        private string GenerateBudgetAnalysisHtml(BudgetReportViewModel model)
        {
            var html = new StringBuilder();

            html.AppendLine("<div class='header'><h1>Budget Analysis Report</h1></div>");

            // Overall Summary
            html.AppendLine("<div class='summary'>");
            html.AppendLine("<h2>Overall Budget Summary</h2>");
            html.AppendLine($"<p>Total Budgeted: <span class='text-right'>${model.TotalBudgeted:N2}</span></p>");
            html.AppendLine($"<p>Total Spent: <span class='text-right'>${model.TotalSpent:N2}</span></p>");
            html.AppendLine($"<p>Total Remaining: <span class='text-right'>${model.TotalRemaining:N2}</span></p>");
            html.AppendLine("</div>");

            if (model.BudgetSummaries.Any())
            {
                // Budget Details Table
                html.AppendLine("<div class='table-section'>");
                html.AppendLine("<h2>Budget Details by Category</h2>");
                html.AppendLine("<table class='table'>");
                html.AppendLine("<tr>");
                html.AppendLine("<th>Category</th>");
                html.AppendLine("<th class='text-right'>Budgeted</th>");
                html.AppendLine("<th class='text-right'>Spent</th>");
                html.AppendLine("<th class='text-right'>Remaining</th>");
                html.AppendLine("<th class='text-right'>Usage %</th>");
                html.AppendLine("</tr>");

                foreach (var budget in model.BudgetSummaries)
                {
                    var rowClass = budget.UsagePercentage > 100 ? "over-budget" :
                                 budget.UsagePercentage > 90 ? "near-limit" : "";

                    html.AppendLine($"<tr class='{rowClass}'>");
                    html.AppendLine($"<td>{budget.Category}</td>");
                    html.AppendLine($"<td class='text-right'>${budget.Budgeted:N2}</td>");
                    html.AppendLine($"<td class='text-right'>${budget.Spent:N2}</td>");
                    html.AppendLine($"<td class='text-right'>${budget.Remaining:N2}</td>");
                    html.AppendLine($"<td class='text-right'>{budget.UsagePercentage:N1}%</td>");
                    html.AppendLine("</tr>");
                }

                html.AppendLine("</table>");
                html.AppendLine("</div>");

                // Monthly Spending Trends if available
                if (model.MonthlySpending.Any() && model.MonthlySpending.First().Value.Any())
                {
                    html.AppendLine("<div class='table-section'>");
                    html.AppendLine("<h2>Monthly Spending Trends</h2>");
                    html.AppendLine("<table class='table'>");

                    // Headers (Months)
                    html.AppendLine("<tr><th>Category</th>");
                    var monthCount = model.MonthlySpending.First().Value.Count;
                    for (int i = 0; i < monthCount; i++)
                    {
                        html.AppendLine($"<th class='text-right'>Month {i + 1}</th>");
                    }
                    html.AppendLine("</tr>");

                    // Data
                    foreach (var category in model.MonthlySpending)
                    {
                        html.AppendLine("<tr>");
                        html.AppendLine($"<td>{category.Key}</td>");
                        foreach (var amount in category.Value)
                        {
                            html.AppendLine($"<td class='text-right'>${amount:N2}</td>");
                        }
                        html.AppendLine("</tr>");
                    }

                    html.AppendLine("</table>");
                    html.AppendLine("</div>");
                }
            }
            else
            {
                html.AppendLine("<p class='text-center'>No budget data available for the selected period.</p>");
            }

            return html.ToString();
        }

        private void GenerateCashFlowExcel(ExcelWorksheet worksheet, CashFlowReportViewModel model)
        {
            try
            {
                // Set title
                worksheet.Cells["A1"].Value = "Cash Flow Report";
                worksheet.Cells["A1:E1"].Merge = true;
                worksheet.Cells["A1:E1"].Style.Font.Bold = true;

                if (!model.Trends.Any())
                {
                    worksheet.Cells["A3"].Value = "No data available for the selected period.";
                    return;
                }

                // Headers
                worksheet.Cells["A3"].Value = "Month";
                worksheet.Cells["B3"].Value = "Income";
                worksheet.Cells["C3"].Value = "Expenses";
                worksheet.Cells["D3"].Value = "Savings";
                worksheet.Cells["E3"].Value = "Savings Rate";
                worksheet.Cells["A3:E3"].Style.Font.Bold = true;

                var row = 4;
                foreach (var trend in model.Trends)
                {
                    worksheet.Cells[$"A{row}"].Value = trend.Month;
                    worksheet.Cells[$"B{row}"].Value = trend.Income;
                    worksheet.Cells[$"C{row}"].Value = trend.Expenses;
                    worksheet.Cells[$"D{row}"].Value = trend.Savings;
                    worksheet.Cells[$"E{row}"].Value = trend.SavingsRate / 100;
                    row++;
                }

                if (row > 4)
                {
                    var dataEndRow = row - 1;
                    worksheet.Cells[$"B4:D{dataEndRow}"].Style.Numberformat.Format = CURRENCY_FORMAT;
                    worksheet.Cells[$"E4:E{dataEndRow}"].Style.Numberformat.Format = PERCENTAGE_FORMAT;

                    // Add borders
                    var dataRange = worksheet.Cells[$"A3:E{dataEndRow}"];
                    var border = dataRange.Style.Border;
                    border.Top.Style = border.Bottom.Style = border.Left.Style = border.Right.Style =
                        OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                }

                // Auto-fit columns if we have data
                if (worksheet.Dimension != null)
                {
                    worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to generate cash flow Excel: {ex.Message}", ex);
            }
        }

        private void GenerateBudgetAnalysisExcel(ExcelWorksheet worksheet, BudgetReportViewModel model)
        {
            try
            {
                // Set title
                worksheet.Cells["A1"].Value = "Budget Analysis Report";
                worksheet.Cells["A1:E1"].Merge = true;
                worksheet.Cells["A1:E1"].Style.Font.Bold = true;

                // Overall Summary
                worksheet.Cells["A3"].Value = "Overall Budget Summary";
                worksheet.Cells["A3"].Style.Font.Bold = true;

                worksheet.Cells["A4"].Value = "Total Budgeted:";
                worksheet.Cells["B4"].Value = model.TotalBudgeted;
                worksheet.Cells["A5"].Value = "Total Spent:";
                worksheet.Cells["B5"].Value = model.TotalSpent;
                worksheet.Cells["A6"].Value = "Total Remaining:";
                worksheet.Cells["B6"].Value = model.TotalRemaining;

                // Format summary amounts
                worksheet.Cells["B4:B6"].Style.Numberformat.Format = CURRENCY_FORMAT;

                if (model.BudgetSummaries.Any())
                {
                    // Budget Details
                    worksheet.Cells["A8"].Value = "Budget Details by Category";
                    worksheet.Cells["A8"].Style.Font.Bold = true;

                    worksheet.Cells["A9"].Value = "Category";
                    worksheet.Cells["B9"].Value = "Budgeted";
                    worksheet.Cells["C9"].Value = "Spent";
                    worksheet.Cells["D9"].Value = "Remaining";
                    worksheet.Cells["E9"].Value = "Usage %";
                    worksheet.Cells["A9:E9"].Style.Font.Bold = true;

                    var row = 10;
                    foreach (var budget in model.BudgetSummaries)
                    {
                        worksheet.Cells[$"A{row}"].Value = budget.Category;
                        worksheet.Cells[$"B{row}"].Value = budget.Budgeted;
                        worksheet.Cells[$"C{row}"].Value = budget.Spent;
                        worksheet.Cells[$"D{row}"].Value = budget.Remaining;
                        worksheet.Cells[$"E{row}"].Value = budget.UsagePercentage / 100;

                        // Conditional formatting
                        if (budget.UsagePercentage > 100)
                        {
                            worksheet.Cells[$"A{row}:E{row}"].Style.Fill.PatternType =
                                OfficeOpenXml.Style.ExcelFillStyle.Solid;
                            worksheet.Cells[$"A{row}:E{row}"].Style.Fill.BackgroundColor
                                .SetColor(System.Drawing.Color.LightPink);
                        }
                        else if (budget.UsagePercentage > 90)
                        {
                            worksheet.Cells[$"A{row}:E{row}"].Style.Fill.PatternType =
                                OfficeOpenXml.Style.ExcelFillStyle.Solid;
                            worksheet.Cells[$"A{row}:E{row}"].Style.Fill.BackgroundColor
                                .SetColor(System.Drawing.Color.LightYellow);
                        }

                        row++;
                    }

                    if (row > 10)
                    {
                        var lastRow = row - 1;
                        worksheet.Cells[$"B10:D{lastRow}"].Style.Numberformat.Format = CURRENCY_FORMAT;
                        worksheet.Cells[$"E10:E{lastRow}"].Style.Numberformat.Format = PERCENTAGE_FORMAT;
                    }
                }

                // Auto-fit columns
                if (worksheet.Dimension != null)
                {
                    worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to generate budget analysis Excel: {ex.Message}", ex);
            }
        }

        // Keep the HTML generation methods as they are since they're working fine
    }
}
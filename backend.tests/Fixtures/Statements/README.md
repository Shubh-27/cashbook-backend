# Synthetic statement fixtures

These committed CSV, XLS, and XLSX files are synthetic parser fixtures; normal tests only read them.
Regenerate the CSV and XLSX by running `StatementFixtureGenerator.Ensure()` from a local developer harness, then commit the outputs.
Regenerate the XLS with `soffice --headless --convert-to xls synthetic-statement.xlsx`; LibreOffice is needed only for that conversion.
Do not replace these fixtures with real bank or account data.

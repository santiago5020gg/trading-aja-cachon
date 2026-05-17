
dotnet run --project csim -c Release -- --trades 3 --perdida-max 500 --modo 1a1
  --colchon 5 --colchon-be 8 --breakeven-pct 35 --no-telemetry "historicos test\2026\MNQ 06-26.Last.txt"

dotnet run --project csim -c Release -- --trades 3 --perdida-max 500 --modo 1a2
  --colchon 5 --colchon-be 5 --breakeven-pct 60 --no-telemetry "historicos test\2026\MNQ 06-26.Last.txt"


# primero cuando hay cambios
dotnet build csim -c Release

   # Terminal 1
  csim\bin\Release\net9.0\csim.exe --trades 3 --perdida-max 500 --modo 1a2 --colchon 5
  --colchon-be 5 --breakeven-pct 60 --no-telemetry "historicos test\2026\MNQ 06-26.Last.txt"

  # Terminal 2
  csim\bin\Release\net9.0\csim.exe --trades 3 --perdida-max 500 --modo 1a1 --colchon 5
  --colchon-be 8 --breakeven-pct 35 --no-telemetry "historicos test\2026\MNQ 06-26.Last.txt"

python gen_report.py csim/output/abr-may-cs5-cbe5-bk60-mt3-pmd500-1a2
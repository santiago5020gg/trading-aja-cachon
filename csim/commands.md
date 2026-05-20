
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


# full parametros
 csim\bin\Release\net9.0\csim.exe --trades 3 --perdida-max 500 --modo 1a2 --colchon 5 --colchon-be 5
  --breakeven-pct 60 --trail-tp1 "75:45,85:60,95:80,99:95" --trail-tp2
  "50:18,70:50,85:60,90:70,95:84,98:94" --no-telemetry "historicos test\2026\MNQ 06-26.Last.txt"

  ## # 4 pares → CantTrailTP1 = 4 (usa los 4 escalones)
  --trail-tp1 "75:45,85:60,95:80,99:95"

  # 2 pares → CantTrailTP1 = 2 (solo usa 2 escalones)
  --trail-tp1 "75:45,95:80"


  # combinaciones a poner a prueba
  Abr-13:
  --trades 2 --perdida-max 500 --modo 1a1 --colchon 5 --colchon-be 5 --breakeven-pct 35
  --trail-tp1 "45:20,65:42,82:62,96:88"
  --trail-tp2 "35:12,55:32,72:52,86:68,94:82,99:94"

  Abr-18:
  --trades 2 --perdida-max 600 --modo 1a1 --colchon 5 --colchon-be 4 --breakeven-pct 30
  --trail-tp1 "38:12,55:30,75:52,92:78"
  --trail-tp2 "28:8,48:25,65:45,80:60,90:75,98:90"

  Abr-14: // esta es la gano +37... va por aqui
  --trades 3 --perdida-max 500 --modo 1a1 --colchon 3 --colchon-be 3 --breakeven-pct 40
  --trail-tp1 "50:25,68:45,84:65,96:88"
  --trail-tp2 "38:14,56:35,74:54,86:68,94:82,99:94"

  Abr-24: // referencia 14.. gano +158
  --trades 3 --perdida-max 600 --modo 1a1 --colchon 3 --colchon-be 3 --breakeven-pct 40
  --trail-tp1 "50:25,68:45,84:65,96:88"
  --trail-tp2 "38:14,56:35,74:54,86:68,94:82,99:94"


  # ganadora hasta ahora
  csim\bin\Release\net9.0\csim.exe --trades 3 --perdida-max 600 --modo 1a1 --colchon 3 --colchon-be 3 --breakeven-pct 40
  --trail-tp1 "50:25,68:45,84:65,96:88"
  --trail-tp2 "38:14,56:35,74:54,86:68,94:82,99:94" --no-telemetry "historicos test\2026\MNQ 06-26.Last.txt"



  # 19/05/2026 full parametros multisesion

  dotnet run --project csim --trades 3 --perdida-max 600 --modo 1a1 --colchon 3 --colchon-be 3
  --breakeven-pct 40 --america --no-telemetry "historicos test\2026\MNQ 06-26.Last.txt"



dotnet run --project csim --trades 10 --perdida-max 1000 --modo 1a1 --colchon 3 --colchon-be 3
  --breakeven-pct 60 --asia --europa --america --no-telemetry "historicos test\2026\MNQ 03-26.Last-001.txt"
# ScanProfinet

Ferramenta de diagnóstico de redes **PROFINET** para uso em campo: descobre dispositivos via DCP, salva "fotografias" da rede, compara configurações e monitora a disponibilidade dos equipamentos por ping.

Aplicação desktop **offline** (Windows / WPF / .NET 8), distribuída por instalador com ícone na área de trabalho.

**Copyright © 2026 Paulo Leal Taveira. Todos os direitos reservados.**

---

## Funcionalidades

### 1. Scan da rede (DCP)
- Descobre dispositivos PROFINET na placa de rede selecionada (protocolo DCP, camada 2).
- Mostra nome, IP, máscara, gateway, MAC, fabricante e função.
- **Atribuir IP / máscara / gateway** a um dispositivo.
- **Atribuir Device Name** (padronizado em minúsculas).
- **Piscar LED** para localizar o equipamento fisicamente.
- **Salvar a rede** com um nome (ex.: `RedeCliente1`) no banco local.

### 2. Comparar redes
- Escolhe uma rede salva como **referência** e escaneia a rede **atual**.
- A inteligência de comparação identifica, pela identidade física (MAC):
  - **SAIU DA REDE** — dispositivo da referência que não responde mais.
  - **ENTROU NA REDE** — dispositivo novo que não constava da referência.
  - **ALTERADO** — mudou IP, nome, gateway ou máscara (mostra o antes → depois).
- Útil para saber se alguém desconectou um inversor ou plugou algo fora do padrão.

### 3. Monitor de rede
- Seleciona dispositivos (do scan ou de uma rede salva) e faz **ping contínuo**.
- Mostra estado ao vivo (**ONLINE / OFFLINE / OSCILANDO**), latência, perda e histórico.
- Registra automaticamente no banco e em arquivo de log:
  - **QUEDA** — parou de responder;
  - **RETORNO** — voltou a responder;
  - **OSCILAÇÃO (flapping)** — sobe/desce repetidamente numa janela de tempo.

---

## Requisitos

- Windows 10 (1809+) ou Windows 11, 64 bits.
- **Npcap** — driver necessário para o scan DCP. O instalador oferece a instalação
  automática (o monitor por ping funciona mesmo sem ele).

## Dados locais

Tudo fica em `%LOCALAPPDATA%\ScanProfinet`:
- `scanprofinet.db` — banco **SQLite** (redes salvas e histórico de eventos);
- `logs\` — logs diários.

Não requer servidor, internet ou licença de banco de dados.

---

## Desenvolvimento

```powershell
# Compilar e rodar
cd ScanProfinet
dotnet run
```

Stack: WPF (.NET 8), CommunityToolkit.Mvvm, SharpPcap/PacketDotNet (DCP),
Microsoft.Data.Sqlite (banco).

## Gerar o instalador

1. (Opcional) Coloque `npcap-x.xx.exe` em `installer\dependencies\`.
2. Rode:

```powershell
cd installer
powershell -ExecutionPolicy Bypass -File build-installer.ps1 -Version 1.0.0
```

Requer [Inno Setup 6](https://jrsoftware.org/isdl.php). O instalador sai em `installer\output\`.

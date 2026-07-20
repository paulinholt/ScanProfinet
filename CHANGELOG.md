# Changelog

Todas as mudanças notáveis do ScanProfinet.

## [Não lançado] — 1.2.0
### Adicionado
- **Aba Topologia (LLDP)**: mapa de vizinhança porta a porta, lido via SNMP
  (LLDP-MIB) de cada dispositivo — mostra quem está ligado em quem. Base para
  detectar cabo trocado. Requer SNMP habilitado nos devices.
- **Comparar duas redes salvas** (ex.: ontem × hoje), além de comparar com o scan atual.
- **Scan**: contagem de escaneados (Total / Com IP / Sem IP) e **grid separado**
  para dispositivos sem IP; coluna Fabricante como 2ª; janela maximizada.

## [1.1.2] — 2026-07-19
### Melhorado
- **Scan confiável em redes grandes**: as respostas dos dispositivos são espalhadas no tempo
  (campo *ResponseDelay* do DCP) e a descoberta é reenviada, acumulando por MAC. Corrige a
  contagem instável (ex.: 319/371/382) em redes com centenas de dispositivos.
- Tempo de escuta padrão passou para **5 s** e há nova opção de **12 s**.
- Nomes de **placa de rede**, **rede de referência** e **fonte** passam a aparecer corretamente
  nos seletores (antes exibia o nome interno da classe).
### Adicionado
- **Limite de 50 dispositivos** no monitor, com contador e botões *Selecionar 50* / *Limpar*.

## [1.1.1] — 2026-07-19
### Corrigido
- **Compatibilidade com Windows Server 2012**: a versão mínima do Windows no instalador foi
  reduzida e o **Visual C++ 2015‑2022** passou a ser embutido (o .NET 8 precisa dele nesse SO).

## [1.1.0] — 2026-07-19
### Adicionado
- **Ícone na bandeja**: minimizar/fechar mantém o monitoramento em segundo plano.
- **Detecção de novos dispositivos** na rede via re-scan DCP periódico.
- **Notificações** na bandeja e dentro do app para quedas e novos dispositivos.
- **Logs dedicados** de reendereçamento e de monitoramento.
- Painel lateral de **redes salvas** com consulta por duplo clique.

## [1.0.0] — 2026-07-18
### Adicionado
- Primeira versão: **Scan DCP** (descoberta, Set IP/Nome, blink LED, salvar rede),
  **Comparação** de redes por MAC e **Monitor** por ping com detecção de oscilação.
- Banco local SQLite e instalador com ícone na área de trabalho.

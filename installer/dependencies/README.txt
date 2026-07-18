Coloque aqui o instalador do Npcap para que ele seja embutido no setup do ScanProfinet.

1. Baixe o Npcap em: https://npcap.com/#download
   (arquivo típico: npcap-1.79.exe)

2. Copie o .exe para esta pasta (installer\dependencies\).

3. Rode  installer\build-installer.ps1  — ele detecta o npcap*.exe automaticamente
   e adiciona a opção "Instalar Npcap" ao instalador, marcada apenas quando o
   driver ainda não estiver presente na máquina.

Sem o Npcap, o scan PROFINET/DCP não funciona (o aplicativo avisa o usuário).
O monitor por ping funciona normalmente sem o Npcap.

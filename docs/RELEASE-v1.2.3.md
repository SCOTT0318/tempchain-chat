# TChat v1.2.3

- 암호 방식 선택은 최초 설정·암호 변경 화면에만 표시
- 잠금 해제 화면에서는 선택 버튼 없이 저장된 4자리·6자리·문자 암호 입력만 표시
- 이전 저장 정보에 암호 방식이 없는 경우에도 선택 버튼을 표시하지 않음
- 이전 암호는 처음 한 번 기존 암호를 입력하면 방식을 기억하고 이후부터 해당 입력칸으로 표시
- 기존 로그인·암호·알림 시간·자동 잠금 설정 유지

## 다운로드

- [TChat 설치 EXE](https://github.com/SCOTT0318/tempchain-chat/releases/download/v1.2.3/TChat-Setup-1.2.3.exe)
- [TChat ZIP](https://github.com/SCOTT0318/tempchain-chat/releases/download/v1.2.3/TChat-1.2.3-win-x64.zip)
- [SHA-256](https://github.com/SCOTT0318/tempchain-chat/releases/download/v1.2.3/SHA256SUMS.txt)

## 검증

코어 회귀 테스트 104개와 WPF 검증을 수행했습니다. 4자리·6자리 및 이전 암호의 잠금 화면에서 방식 선택이 숨겨지는지, 이전 암호 확인 후 저장된 방식이 복원되는지 확인했습니다.

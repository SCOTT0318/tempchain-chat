# TChat v1.2.2

- 잠금 암호 설정에 **4자리로 하기 / 6자리로 하기 / 문자 포함** 선택 추가
- 숫자 암호는 선택한 자리수만큼 독립된 입력칸 표시
- 잠금 해제 시 저장된 암호 방식에 따라 4칸 또는 6칸 표시
- 한 자리 입력 후 다음 칸 이동, 백스페이스·방향키 수정, 숫자 붙여넣기 지원
- 숫자 칸에서는 문자와 자리수를 초과한 입력 거부
- 문자 포함 암호는 기존처럼 6자 이상 입력
- 기존 로그인·암호·알림 시간·자동 잠금 설정 유지

이전 버전에서 설정한 암호는 그대로 사용할 수 있습니다. 이전 저장 정보에는 암호 방식이 없으므로 처음 잠금 해제 시 기존 방식(4자리·6자리·문자 포함)을 선택합니다. 정상 해제 후에는 해당 방식을 기억합니다.

## 다운로드

- [TChat 설치 EXE](https://github.com/SCOTT0318/tempchain-chat/releases/download/v1.2.2/TChat-Setup-1.2.2.exe)
- [TChat ZIP](https://github.com/SCOTT0318/tempchain-chat/releases/download/v1.2.2/TChat-1.2.2-win-x64.zip)
- [SHA-256](https://github.com/SCOTT0318/tempchain-chat/releases/download/v1.2.2/SHA256SUMS.txt)

## 검증

코어 회귀 테스트 104개와 WPF 검증을 수행했습니다. 4칸·6칸 선택 및 설정·해제, 저장된 방식 복원, 문자 입력·초과 길이 거부, 기존 암호의 방식 이행과 알림 시간 보존을 확인했습니다. 실제 잠금·설정 화면을 렌더링해 검토했습니다.

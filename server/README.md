# TChat ERP 접근 검사

공개 경로는 기존 `https://chat.tempchain.myds.me:443`입니다. DSM 역방향 프록시의 TChat 대상만 `http://127.0.0.1:8091`로 연결합니다. 기존 ERP 및 moa-erp-intro 경로는 유지합니다. 8091은 호스트 루프백에만 바인딩하므로 공유기 포트 개방이 필요하지 않습니다.

## 배포 구성

- Container Manager 프로젝트 `tchat-access`, 경로 `/volume1/docker/tchat-access`
- `compose.access.yaml`을 프로젝트 YAML에 입력하고 빌드·시작
- `/volume1/docker/tchat-access/data`를 컨테이너 `/directory`에 읽기 전용 연결
- DSM 작업 스케줄러 `TChat ERP Directory`: 매일 00:00~23:59, 1분 간격, 활성화
- 작업 스크립트는 `refresh-directory-task.sh`; 이메일 조회 실행 권한 때문에 사용자 승인 후 해당 작업만 root 실행
- 컨테이너는 UID 1000, 읽기 전용 파일 시스템, 권한 제거, 메모리 128 MB. DS925+에서는 CPU CFS 제한을 지원하지 않아 설정하지 않음. 커널 미지원 PIDs 제한은 실행 시 무시됨.

원본은 실제 `erp` 그룹과 DSM 계정 정보입니다. 작업은 이름·직급 설명, 이메일, 그룹을 조회해 `identity.json`을 원자적으로 교체합니다. 암호·세션·synouser 원본 출력은 저장하지 않습니다. `email-status.txt`에는 조회 성공·실패 유형의 집계만 저장합니다. 서버 계정·암호·ERP 데이터·메시지는 수정하지 않습니다.

그룹이 제외된 계정은 다음 스냅샷부터 기존 세션을 포함해 요청마다 차단합니다(460). administrators 그룹도 예외가 없습니다. 앱은 접근 제한 시 로그아웃하고 저장 로그인을 제거합니다. 디렉터리가 180초 이상 오래됐거나 읽을 수 없으면 사용을 중단합니다(461). 이 일시적 장애에는 앱이 잠기되 저장 로그인은 보존됩니다.

계정 이름 없는 Chat 로그인 응답은 인증된 사용자 목록의 동일 사용자 ID와 대조합니다. 서버 재시작 후에도 임의 계정 힌트를 신뢰하지 않고 기존 세션을 다시 검증합니다. 사용자 목록은 ERP 구성원만 반환하고 계정 설명·이메일을 원본 값으로 보정합니다. 스냅샷 이메일이 비어 있을 때만 기존 Chat 이메일로 대체합니다.

이 호스트는 네이티브 TChat API 전용입니다. `/healthz`와 허용된 POST `/webapi/*.cgi`만 처리하며 DSM 화면·다른 경로·인코딩된 우회 경로는 전달하지 않습니다. 공식 웹·모바일 Chat의 대체 프록시로 사용하지 않습니다. 직접 DSM/공식 Chat 권한은 별도 관리 대상입니다.

## 검증

```powershell
node --test server/access-gateway.test.mjs
```

`/healthz`의 `ok`, ERP 인원수, 스냅샷 나이를 확인한 뒤 공개 HTTPS 경로의 앱 로그인·프로필을 검증합니다. `.github/workflows/external-connectivity.yml`은 사외 GitHub 실행 환경에서 TLS, API discovery, 무인증 접근 거절, ERP 미가입 로그인 거절을 검사하며 실제 계정 암호나 메시지를 보내지 않습니다.

브라우저 관리 화면으로 배포합니다. 저장된 ERP 계정 암호를 SSH에 사용하지 않습니다. 생성한 compose의 인라인 JavaScript와 작업 스크립트는 각각 `access-gateway.mjs`, `export-directory.py`와 일치해야 합니다.

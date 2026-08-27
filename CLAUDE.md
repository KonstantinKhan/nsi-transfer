# Global profile

## Инварианты

- Перед своей работой обязательно активировать `caveman`
- Для поисковых задач (find/locate/search) — использовать субагент `caveman:cavecrew-investigator` (встроен в caveman, компрессированный output). 
- Для остальных investigation/analysis/explorer — STRICT обязательно передавать инструкции явно в промпт:
	- Use caveman mode (full): drop articles, fragments OK, short synonyms, no decorative tables/emoji
	- Use wiki-read to search for project info
	- Use ast-index for code search: ast-index search <query>, ast-index file <pattern>, ast-index symbol, ast-index callers
	- Запрещено использовать рефлексию поиска информации. В случае такой потребности нужно через `AskUseQuetstion` сообщить зачем это нужно и запросить явное согласие пользователя 
-  точка входа в wiki `/docs/wiki/index.md`

## Верификация

- Верификация - только проверка, что сборка прошла без ошибок
- Пользователь самостоятельно проверят корректность работы 
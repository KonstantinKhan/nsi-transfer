export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  extensions?: Record<string, unknown>;
}

const extractErrorText = (data: ProblemDetails): string => {
  if (!data) return '';
  // Если есть detail — берём его, иначе fallback на title
  return (data.detail && data.detail.trim()) || (data.title && data.title.trim()) || '';
};

/**
 * Обрабатывает HTTP-ответ с ошибкой, извлекая детальное сообщение из тела ответа.
 * Генерирует исключение с форматированным сообщением.
 * @param response Объект Response от fetch, у которого response.ok === false.
 * @param fallbackMessage Сообщение по умолчанию, если не удалось извлечь детали из ответа.
 * @returns Promise<never> - функция всегда выбрасывает исключение.
 */
export const handleApiError = async (response: Response, fallbackMessage: string): Promise<never> => {
  let serverMessage = '';

  try {
    const contentType = response.headers.get('content-type') || '';
    if (contentType.includes('application/json') || contentType.includes('application/problem+json')) {
      const errData = (await response.json()) as ProblemDetails;
      serverMessage = extractErrorText(errData);
    } else {
      const textBody = await response.text();
      serverMessage = textBody.slice(0, 500); // Ограничиваем размер, чтобы не выводить огромные HTML-страницы
    }
  } catch (parseError) {
    console.warn('Не удалось прочитать тело ошибки:', parseError);
  }

  const httpInfo = `HTTP ${response.status}${response.statusText ? ' ' + response.statusText : ''}`;
  const finalMessage = serverMessage
    ? `${fallbackMessage} [${httpInfo}]: ${serverMessage}`
    : `${fallbackMessage} [${httpInfo}]`;

  throw new Error(finalMessage);
};
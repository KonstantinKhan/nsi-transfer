using NsiTransfer.Contract.Models.Common;

namespace NsiTransfer.Contract;

public static class ResultExtensions
{
    public static Result<TOut> Bind<TIn, TOut>(this Result<TIn> previousResult, Func<TIn, Result<TOut>> nextFunc)
    {
        if (!previousResult.IsSuccess)
        {
            return previousResult.ErrorMessage;
        }

        var nextResult = nextFunc(previousResult.Data!);
        return nextResult;
    }

    public static async Task<Result<TOut>> BindAsync<TIn, TOut>(this Task<Result<TIn>> previousTask, Func<TIn, Task<Result<TOut>>> nextFunc)
    {
        var previousResult = await previousTask;
        
        if (!previousResult.IsSuccess)
        {
            return previousResult.ErrorMessage;
        }

        var nextResult = await nextFunc(previousResult.Data!);
        return nextResult;
    }

    public static async Task<Result> BindAsync<TIn>(this Task<Result<TIn>> previousTask, Func<TIn, Task<Result>> nextFunc)
    {
        var previousResult = await previousTask;

        if (!previousResult.IsSuccess)
        {
            return previousResult.ErrorMessage;
        }

        var nextResult = await nextFunc(previousResult.Data!);
        return nextResult;
    }

    /// <summary>
    /// Синхронная версия And для простых случаев.
    /// </summary>
    public static Result<(T1, T2)> And<T1, T2>(this Result<T1> first, Func<T1, Result<T2>> secondFunc)
    {
        if (!first.IsSuccess) return first.ErrorMessage;

        var second = secondFunc(first.Data!);
        return second.IsSuccess
            ? (first.Data!, second.Data!)
            : second.ErrorMessage;
    }

    public static Result<TResultNamed> And<T1, T2, TResultNamed>(this Result<T1> first, Func<T1, Result<T2>> secondFunc, Func<T1, T2, TResultNamed> nameFunc)
    {
        if (!first.IsSuccess) return first.ErrorMessage;

        var second = secondFunc(first.Data!);
        return second.IsSuccess
            ? nameFunc(first.Data!, second.Data!)
            : second.ErrorMessage;
    }

    public static async Task<Result<TResultNamed>> AndWithAsync<T1, T2, TResultNamed>(this Result<T1> first, Func<T1, Task<Result<T2>>> secondFunc, Func<T1, T2, TResultNamed> nameFunc)
    {
        if (!first.IsSuccess) return first.ErrorMessage;

        var second = await secondFunc(first.Data!);
        return second.IsSuccess
            ? nameFunc(first.Data!, second.Data!)
            : second.ErrorMessage;
    }

    /// <summary>
    /// Выполняет вторую операцию и возвращает кортеж из результатов обеих.
    /// Если первая операция провалилась — возвращает её ошибку.
    /// </summary>
    public static async Task<Result<(T1, T2)>> AndAsync<T1, T2>(this Task<Result<T1>> firstTask, Func<T1, Task<Result<T2>>> secondFunc)
    {
        var first = await firstTask;
        if (!first.IsSuccess) return first.ErrorMessage;

        var second = await secondFunc(first.Data!);
        return second.IsSuccess
            ? (first.Data!, second.Data!)
            : second.ErrorMessage;
    }

    public static async Task<Result<TResultNamed>> AndAsync<T1, T2, TResultNamed>(this Task<Result<T1>> firstTask, Func<T1, Task<Result<T2>>> secondFunc, Func<T1, T2, TResultNamed> nameFunc)
    {
        var first = await firstTask;
        if (!first.IsSuccess) return first.ErrorMessage;

        var second = await secondFunc(first.Data!);
        return second.IsSuccess
            ? nameFunc(first.Data!, second.Data!)
            : second.ErrorMessage;
    }

    public static Result<TOut> Map<TIn, TOut>(this Result<TIn> previousResult, Func<TIn, TOut> mapFunc)
    {
        if (!previousResult.IsSuccess)
        {
            return previousResult.ErrorMessage;
        }

        var mappedValue = mapFunc(previousResult.Data!);
        return mappedValue;
    }

    public static async Task<Result<TOut>> MapAsync<TIn, TOut>(this Task<Result<TIn>> previousTask, Func<TIn, TOut> mapFunc)
    {
        var previusResult = await previousTask;

        if (!previusResult.IsSuccess)
        {
            return previusResult.ErrorMessage;
        }

        var mappedValue = mapFunc(previusResult.Data!);
        return mappedValue;
    }




    public static Result<T> OnFailure<T>(this Result<T> result, Action<string> actionWithErrorMessage)
    {
        if (!result.IsSuccess)
        {
            actionWithErrorMessage(result.ErrorMessage);
        }

        return result;
    }

    public static async Task<Result<T>> OnFailureAsync<T>(this Task<Result<T>> task, Action<string> actionWithErrorMessage)
    {
        var result = await task;
        if (!result.IsSuccess)
        {
            actionWithErrorMessage(result.ErrorMessage);
        }

        return result;
    }

    public static async Task<Result> OnFailureAsync(this Task<Result> task, Func<string, Task> asyncAction)
    {
        var result = await task;
        if (!result.IsSuccess)
        {
            await asyncAction(result.ErrorMessage);
        }
        return result;
    }

    /// <summary>
    /// Выполняет действие при ошибке для обобщённого Result{T}, передавая сам результат.
    /// </summary>
    public static Result<T> OnFailure<T>(this Result<T> result, Action<Result<T>> action)
    {
        if (!result.IsSuccess)
        {
            action(result);
        }
        return result;
    }

    /// <summary>
    /// Асинхронная версия OnFailure для обобщённого Result{T}.
    /// </summary>
    public static async Task<Result<T>> OnFailureAsync<T>(this Task<Result<T>> task, Action<Result<T>> action)
    {
        var result = await task;
        if (!result.IsSuccess)
        {
            action(result);
        }
        return result;
    }

    /// <summary>
    /// Асинхронное действие при ошибке для обобщённого Result{T}.
    /// </summary>
    public static async Task<Result<T>> OnFailureAsync<T>(this Task<Result<T>> task, Func<Result<T>, Task> asyncAction)
    {
        var result = await task;
        if (!result.IsSuccess)
        {
            await asyncAction(result);
        }
        return result;
    }



    // ========== НОВЫЕ: Расширения для не-обобщённого Result ==========

    /// <summary>
    /// Цепочка операций: если предыдущий результат успешен, выполняет следующую функцию.
    /// </summary>
    public static Result Bind(this Result previousResult, Func<Result> nextFunc)
    {
        if (!previousResult.IsSuccess)
        {
            return previousResult;
        }

        return nextFunc();
    }

    /// <summary>
    /// Асинхронная цепочка операций для не-обобщённого Result.
    /// </summary>
    public static async Task<Result> BindAsync(this Task<Result> previousTask, Func<Task<Result>> nextFunc)
    {
        var previousResult = await previousTask;

        if (!previousResult.IsSuccess)
        {
            return previousResult;
        }

        return await nextFunc();
    }

    /// <summary>
    /// Цепочка: Result -> Result{TOut} (преобразует успех в значение).
    /// </summary>
    public static Result<TOut> Bind<TOut>(this Result previousResult, Func<Result<TOut>> nextFunc)
    {
        if (!previousResult.IsSuccess)
        {
            return previousResult.ErrorMessage;
        }

        return nextFunc();
    }

    /// <summary>
    /// Асинхронная цепочка: Task{Result} -> Task{Result{TOut}}.
    /// </summary>
    public static async Task<Result<TOut>> BindAsync<TOut>(this Task<Result> previousTask, Func<Task<Result<TOut>>> nextFunc)
    {
        var previousResult = await previousTask;

        if (!previousResult.IsSuccess)
        {
            return previousResult.ErrorMessage;
        }

        return await nextFunc();
    }

    /// <summary>
    /// Преобразует успешный не-обобщённый результат в значение.
    /// </summary>
    public static Result<TOut> Map<TOut>(this Result previousResult, Func<TOut> mapFunc)
    {
        if (!previousResult.IsSuccess)
        {
            return previousResult.ErrorMessage;
        }

        return mapFunc();
    }

    /// <summary>
    /// Асинхронная версия Map для не-обобщённого Result.
    /// </summary>
    public static async Task<Result<TOut>> MapAsync<TOut>(this Task<Result> previousTask, Func<TOut> mapFunc)
    {
        var previousResult = await previousTask;

        if (!previousResult.IsSuccess)
        {
            return previousResult.ErrorMessage;
        }

        return mapFunc();
    }

    /// <summary>
    /// Выполняет действие при ошибке для не-обобщённого Result.
    /// </summary>
    public static Result OnFailure(this Result result, Action<string> actionWithErrorMessage)
    {
        if (!result.IsSuccess)
        {
            actionWithErrorMessage(result.ErrorMessage);
        }

        return result;
    }

    /// <summary>
    /// Асинхронная версия OnFailure для не-обобщённого Result.
    /// </summary>
    public static async Task<Result> OnFailureAsync(this Task<Result> task, Action<string> actionWithErrorMessage)
    {
        var result = await task;
        if (!result.IsSuccess)
        {
            actionWithErrorMessage(result.ErrorMessage);
        }

        return result;
    }


    /// <summary>
    /// Выполняет действие при ошибке, передавая сам объект Result.
    /// Позволяет модифицировать ошибку (добавить контекст, логировать и т.д.).
    /// </summary>
    public static Result OnFailure(this Result result, Action<Result> action)
    {
        if (!result.IsSuccess)
        {
            action(result);
        }
        return result;
    }

    /// <summary>
    /// Асинхронная версия OnFailure с передачей Result для не-обобщённого типа.
    /// </summary>
    public static async Task<Result> OnFailureAsync(this Task<Result> task, Action<Result> action)
    {
        var result = await task;
        if (!result.IsSuccess)
        {
            action(result);
        }
        return result;
    }

    /// <summary>
    /// Асинхронное действие при ошибке для не-обобщённого Result.
    /// </summary>
    public static async Task<Result> OnFailureAsync(this Task<Result> task, Func<Result, Task> asyncAction)
    {
        var result = await task;
        if (!result.IsSuccess)
        {
            await asyncAction(result);
        }
        return result;
    }



    /// <summary>
    /// Выполняет действие при успехе (side-effect) для не-обобщённого Result.
    /// Полезно для логирования или триггеров без изменения результата.
    /// </summary>
    public static Result OnSuccess(this Result result, Action onSuccess)
    {
        if (result.IsSuccess)
        {
            onSuccess();
        }

        return result;
    }

    /// <summary>
    /// Асинхронная версия OnSuccess для не-обобщённого Result.
    /// </summary>
    public static async Task<Result> OnSuccessAsync(this Task<Result> task, Action onSuccess)
    {
        var result = await task;
        if (result.IsSuccess)
        {
            onSuccess();
        }

        return result;
    }

    /// <summary>
    /// Выполняет асинхронное действие при успехе для не-обобщённого Result.
    /// </summary>
    public static async Task<Result> OnSuccessAsync(this Task<Result> task, Func<Task> onSuccess)
    {
        var result = await task;
        if (result.IsSuccess)
        {
            await onSuccess();
        }

        return result;
    }
}
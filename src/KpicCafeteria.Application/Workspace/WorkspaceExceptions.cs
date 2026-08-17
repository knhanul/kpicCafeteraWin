namespace KpicCafeteria.Application.Workspace;

/// <summary>
/// 주간 급식 운영 업무 오류의 기본 타입.
/// UI는 이 예외의 Message를 사용자에게 그대로 표시한다.
/// </summary>
public class WorkspaceException : Exception
{
    public WorkspaceException(string message)
        : base(message)
    {
    }
}

/// <summary>주말에는 배식을 새로 만들 수 없을 때.</summary>
public sealed class WeekendServiceNotAllowedException : WorkspaceException
{
    public WeekendServiceNotAllowedException()
        : base("기본 화면에서는 평일 배식만 작성합니다.")
    {
    }
}

/// <summary>배식유형을 찾을 수 없을 때.</summary>
public sealed class MealTypeSettingNotFoundException : WorkspaceException
{
    public MealTypeSettingNotFoundException()
        : base("사용 가능한 배식유형이 아닙니다.")
    {
    }
}

/// <summary>배식을 찾을 수 없을 때.</summary>
public sealed class MealServiceNotFoundException : WorkspaceException
{
    public MealServiceNotFoundException()
        : base("배식을 찾을 수 없습니다.")
    {
    }
}

/// <summary>메뉴를 찾을 수 없거나 비활성일 때.</summary>
public sealed class MenuNotFoundException : WorkspaceException
{
    public MenuNotFoundException(int menuId)
        : base($"메뉴를 찾을 수 없습니다: {menuId}")
    {
    }
}

/// <summary>이미 식단에 추가된 메뉴일 때.</summary>
public sealed class MenuAlreadyAddedException : WorkspaceException
{
    public MenuAlreadyAddedException()
        : base("이미 식단에 추가된 메뉴입니다.")
    {
    }
}

/// <summary>요청에 중복 메뉴가 있을 때.</summary>
public sealed class DuplicateMenuInRequestException : WorkspaceException
{
    public DuplicateMenuInRequestException()
        : base("요청에 중복된 메뉴가 있습니다.")
    {
    }
}

/// <summary>요청에 중복 정렬 순서가 있을 때.</summary>
public sealed class DuplicateSortOrderInRequestException : WorkspaceException
{
    public DuplicateSortOrderInRequestException()
        : base("요청에 중복된 정렬 순서가 있습니다.")
    {
    }
}

/// <summary>추가할 메뉴가 없을 때.</summary>
public sealed class EmptyMenuSelectionException : WorkspaceException
{
    public EmptyMenuSelectionException()
        : base("추가할 메뉴를 선택해 주세요.")
    {
    }
}

/// <summary>선택한 레시피가 해당 메뉴의 레시피가 아닐 때.</summary>
public sealed class RecipeNotInMenuException : WorkspaceException
{
    public RecipeNotInMenuException(string menuName)
        : base($"{menuName}의 선택한 레시피를 찾을 수 없습니다.")
    {
    }
}

/// <summary>단건 추가 시 선택한 레시피가 해당 메뉴의 사용 가능한 레시피가 아닐 때.</summary>
public sealed class RecipeNotAvailableException : WorkspaceException
{
    public RecipeNotAvailableException()
        : base("선택한 메뉴의 사용 가능한 레시피가 아닙니다.")
    {
    }
}

/// <summary>선택한 레시피가 사용 중지 상태일 때.</summary>
public sealed class RecipeInactiveException : WorkspaceException
{
    public RecipeInactiveException(string menuName)
        : base($"{menuName}의 선택한 레시피는 사용 중지 상태입니다.")
    {
    }
}

/// <summary>식단 메뉴를 찾을 수 없을 때.</summary>
public sealed class ServiceMenuNotFoundException : WorkspaceException
{
    public ServiceMenuNotFoundException()
        : base("식단 메뉴를 찾을 수 없습니다.")
    {
    }
}

/// <summary>계획식수가 음수일 때.</summary>
public sealed class InvalidPlannedCountException : WorkspaceException
{
    public InvalidPlannedCountException()
        : base("계획식수는 0 이상이어야 합니다.")
    {
    }
}

/// <summary>배식시간 형식이 잘못되었을 때.</summary>
public sealed class InvalidServiceTimeException : WorkspaceException
{
    public InvalidServiceTimeException()
        : base("배식시간 형식은 HH:MM이어야 합니다.")
    {
    }
}

/// <summary>메뉴 순서 변경 시 목록이 일치하지 않을 때.</summary>
public sealed class MenuListMismatchException : WorkspaceException
{
    public MenuListMismatchException()
        : base("메뉴 목록이 현재 식단과 일치하지 않습니다.")
    {
    }
}

/// <summary>실제 식수가 음수일 때.</summary>
public sealed class InvalidActualCountException : WorkspaceException
{
    public InvalidActualCountException()
        : base("실제 식수는 0 이상이어야 합니다.")
    {
    }
}

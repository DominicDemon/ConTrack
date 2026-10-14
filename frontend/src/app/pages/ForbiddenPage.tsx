import { Button, Result } from 'antd';
import { Link } from 'react-router-dom';

/** III.1.8 Status pages – No Permission (MSG30). */
export function ForbiddenPage() {
  return (
    <Result
      status="403"
      title="403"
      subTitle="Bạn không có quyền truy cập trang này."
      extra={
        <Link to="/">
          <Button type="primary">Về trang chủ</Button>
        </Link>
      }
    />
  );
}

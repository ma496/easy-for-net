import {openModal} from "@mantine/modals";
import {Text} from "@mantine/core";
import React from "react";

interface ErrorModal {
  status: number,
  title: string,
  message?: string,
  errors?: string[]
}

export const showError = (error: any) => {
  const err = extractError(error);
  return openModal({
    title: <Text color={'red'}>{err.title}</Text>,
    children: (
      err.status !== 422
        ? <Text>{err.message}</Text>
        : (
          err.errors?.map((e, i) => (
            <Text key={i}>{e}</Text>
          ))
        )
    ),
  });
};

const extractError = (err: any): ErrorModal => {
  const body = typeof err.body === 'object' ? err.body : JSON.parse(err.body);

  if (err.status === 422) {
    let errors: string[] = [];
    for (let p in body.errors) {
      body.errors[p].forEach((e: any) => errors.push(e));
    }
    return {
      status: err.status,
      title: body.title,
      errors: errors
    }
  } else if (err.status === 400) {
    return {
      status: err.status,
      title: 'Bad Request',
      message: body.message
    }
  } else {
    return {
      status: err.status,
      title: 'Server Error',
      message: body.message
    }
  }
}
